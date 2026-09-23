#include "AutoUpdateChecker.h"
#include "ActivationCodeDialog.h"

#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QHostInfo>
#include <QJsonDocument>
#include <QJsonObject>
#include <QMessageBox>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QNetworkRequest>
#include <QProcess>
#include <QPushButton>
#include <QSaveFile>
#include <QStandardPaths>
#include <QUrl>
#include <QtGlobal>

#ifdef Q_OS_WIN
#include <windows.h>
#include <shellapi.h>
#endif

namespace
{
    /*
     * 安装包内的非敏感配置。
     *
     * 注意：这里没有 UpdateToken。
     */
    struct BootstrapConfig
    {
        QString serverUrl;
        QString softwareCode;
        QString versionFile;
        QString mainExecutable;
        bool fallbackToFullInstaller = true;
        bool restartAfterUpdate = true;
        int waitForProcessSeconds = 60;
    };

    /*
     * 激活成功后真正使用的更新配置。
     *
     * updater.json 会包含设备独立 UpdateToken，
     * 所以不应该预置进安装包，也不要提交真实文件到 Git。
     */
    struct UpdaterConfig
    {
        QString serverUrl;
        QString softwareCode;
        QString updateToken;
        QString versionFile;
        QString mainExecutable;
        bool fallbackToFullInstaller = true;
        bool restartAfterUpdate = true;
        int waitForProcessSeconds = 60;

        /*
         * 仅供业务程序内部使用，
         * 不写进 JSON。
         */
        QString configFilePath;
    };

    QString trimBaseUrl(
        QString value)
    {
        value = value.trimmed();

        while (value.endsWith('/'))
        {
            value.chop(1);
        }

        return value;
    }

    bool isValidHttpUrl(
        const QString &value)
    {
        const QUrl url(value);

        if (!url.isValid())
        {
            return false;
        }

        return url.scheme().compare(
                   QStringLiteral("http"),
                   Qt::CaseInsensitive) == 0
            || url.scheme().compare(
                   QStringLiteral("https"),
                   Qt::CaseInsensitive) == 0;
    }

    /*
     * 生成一个可以安全作为 Windows 目录名的软件编码。
     */
    QString makeSafePathSegment(
        QString value)
    {
        value = value.trimmed();

        if (value.isEmpty())
        {
            return QStringLiteral("default");
        }

        const QString invalidCharacters =
            QStringLiteral("<>:\"/\\|?*");

        for (int i = 0;
             i < invalidCharacters.size();
             ++i)
        {
            value.replace(
                invalidCharacters.at(i),
                QLatin1Char('_'));
        }

        return value;
    }

    /*
     * 新版 updater.json 放到当前 Windows 用户可写目录：
     *
     * %LOCALAPPDATA%\SoftwareServicePlatform\UpdaterConfigs\<softwareCode>\updater.json
     *
     * 这样即使业务软件安装在 Program Files，
     * 首次更新授权也不需要管理员权限才能保存设备 Token。
     */
    QString getWritableConfigPath(
        const QString &softwareCode)
    {
        QString localAppData =
            QString::fromLocal8Bit(
                qgetenv("LOCALAPPDATA"))
                .trimmed();

        if (localAppData.isEmpty())
        {
            localAppData =
                QStandardPaths::writableLocation(
                    QStandardPaths::GenericDataLocation);
        }

        return QDir(localAppData)
            .filePath(
                QStringLiteral(
                    "SoftwareServicePlatform/UpdaterConfigs/%1/updater.json")
                    .arg(
                        makeSafePathSegment(
                            softwareCode)));
    }

    bool readJsonObject(
        const QString &filePath,
        QJsonObject &object,
        QString &errorMessage)
    {
        QFile file(filePath);

        if (!file.open(QIODevice::ReadOnly))
        {
            errorMessage =
                QStringLiteral("无法读取配置文件：\n")
                + filePath;

            return false;
        }

        QJsonParseError parseError;

        const QJsonDocument document =
            QJsonDocument::fromJson(
                file.readAll(),
                &parseError);

        file.close();

        if (parseError.error
                != QJsonParseError::NoError
            || !document.isObject())
        {
            errorMessage =
                QStringLiteral("JSON 配置格式无效：\n")
                + parseError.errorString();

            return false;
        }

        object = document.object();
        return true;
    }

    bool loadBootstrapConfig(
        const QString &filePath,
        BootstrapConfig &config,
        QString &errorMessage)
    {
        QJsonObject object;

        if (!readJsonObject(
                filePath,
                object,
                errorMessage))
        {
            return false;
        }

        config.serverUrl =
            trimBaseUrl(
                object.value(
                    QStringLiteral("serverUrl"))
                    .toString());

        config.softwareCode =
            object.value(
                QStringLiteral("softwareCode"))
                .toString()
                .trimmed();

        config.versionFile =
            object.value(
                QStringLiteral("versionFile"))
                .toString()
                .trimmed();

        config.mainExecutable =
            object.value(
                QStringLiteral("mainExecutable"))
                .toString()
                .trimmed();

        config.fallbackToFullInstaller =
            object.value(
                QStringLiteral(
                    "fallbackToFullInstaller"))
                .toBool(true);

        config.restartAfterUpdate =
            object.value(
                QStringLiteral(
                    "restartAfterUpdate"))
                .toBool(true);

        config.waitForProcessSeconds =
            object.value(
                QStringLiteral(
                    "waitForProcessSeconds"))
                .toInt(60);

        if (config.versionFile.isEmpty())
        {
            config.versionFile =
                QStringLiteral("version.txt");
        }

        if (config.waitForProcessSeconds <= 0)
        {
            config.waitForProcessSeconds = 60;
        }

        if (config.serverUrl.isEmpty()
            || config.softwareCode.isEmpty())
        {
            errorMessage =
                QStringLiteral(
                    "updater.bootstrap.json 配置不完整，必须包含：\n"
                    "serverUrl / softwareCode");

            return false;
        }

        if (!isValidHttpUrl(
                config.serverUrl))
        {
            errorMessage =
                QStringLiteral(
                    "updater.bootstrap.json 的 serverUrl 无效：\n")
                + config.serverUrl;

            return false;
        }

        return true;
    }

    bool tryLoadUpdaterConfig(
        const QString &filePath,
        UpdaterConfig &config)
    {
        if (!QFileInfo::exists(filePath))
        {
            return false;
        }

        QJsonObject object;
        QString errorMessage;

        if (!readJsonObject(
                filePath,
                object,
                errorMessage))
        {
            return false;
        }

        config.serverUrl =
            trimBaseUrl(
                object.value(
                    QStringLiteral("serverUrl"))
                    .toString());

        config.softwareCode =
            object.value(
                QStringLiteral("softwareCode"))
                .toString()
                .trimmed();

        config.updateToken =
            object.value(
                QStringLiteral("updateToken"))
                .toString()
                .trimmed();

        config.versionFile =
            object.value(
                QStringLiteral("versionFile"))
                .toString()
                .trimmed();

        config.mainExecutable =
            object.value(
                QStringLiteral("mainExecutable"))
                .toString()
                .trimmed();

        config.fallbackToFullInstaller =
            object.value(
                QStringLiteral(
                    "fallbackToFullInstaller"))
                .toBool(true);

        config.restartAfterUpdate =
            object.value(
                QStringLiteral(
                    "restartAfterUpdate"))
                .toBool(true);

        config.waitForProcessSeconds =
            object.value(
                QStringLiteral(
                    "waitForProcessSeconds"))
                .toInt(60);

        config.configFilePath =
            filePath;

        if (config.serverUrl.isEmpty()
            || config.softwareCode.isEmpty()
            || config.updateToken.isEmpty())
        {
            return false;
        }

        return isValidHttpUrl(
            config.serverUrl);
    }

    bool saveUpdaterConfig(
        const QString &filePath,
        const BootstrapConfig &bootstrap,
        const QString &updateToken,
        QString &errorMessage)
    {
        const QFileInfo fileInfo(filePath);

        QDir directory =
            fileInfo.absoluteDir();

        if (!directory.exists()
            && !QDir().mkpath(
                directory.absolutePath()))
        {
            errorMessage =
                QStringLiteral("无法创建自动更新配置目录：\n")
                + directory.absolutePath();

            return false;
        }

        QJsonObject object;

        object.insert(
            QStringLiteral("serverUrl"),
            bootstrap.serverUrl);

        object.insert(
            QStringLiteral("softwareCode"),
            bootstrap.softwareCode);

        object.insert(
            QStringLiteral("updateToken"),
            updateToken);

        object.insert(
            QStringLiteral("versionFile"),
            bootstrap.versionFile.isEmpty()
                ? QStringLiteral("version.txt")
                : bootstrap.versionFile);

        object.insert(
            QStringLiteral("mainExecutable"),
            bootstrap.mainExecutable);

        object.insert(
            QStringLiteral(
                "fallbackToFullInstaller"),
            bootstrap.fallbackToFullInstaller);

        object.insert(
            QStringLiteral(
                "restartAfterUpdate"),
            bootstrap.restartAfterUpdate);

        object.insert(
            QStringLiteral(
                "waitForProcessSeconds"),
            bootstrap.waitForProcessSeconds <= 0
                ? 60
                : bootstrap.waitForProcessSeconds);

        QSaveFile file(filePath);

        if (!file.open(QIODevice::WriteOnly))
        {
            errorMessage =
                QStringLiteral("无法写入自动更新配置：\n")
                + filePath;

            return false;
        }

        const QByteArray json =
            QJsonDocument(object)
                .toJson(
                    QJsonDocument::Indented);

        if (file.write(json)
            != json.size())
        {
            file.cancelWriting();

            errorMessage =
                QStringLiteral("自动更新配置写入不完整：\n")
                + filePath;

            return false;
        }

        if (!file.commit())
        {
            errorMessage =
                QStringLiteral("自动更新配置保存失败：\n")
                + filePath;

            return false;
        }

        return true;
    }

    bool startUpdaterDetached(
        const QString &updaterExePath,
        const QString &appRootPath,
        const QString &configFilePath)
    {
        const QString pid =
            QString::number(
                QCoreApplication::applicationPid());

#ifdef Q_OS_WIN
        QString escapedAppRoot =
            appRootPath;

        escapedAppRoot.replace(
            QStringLiteral("\""),
            QStringLiteral("\\\""));

        QString escapedConfigPath =
            configFilePath;

        escapedConfigPath.replace(
            QStringLiteral("\""),
            QStringLiteral("\\\""));

        /*
         * 关键变化：
         * 显式把 LocalAppData 中的 updater.json
         * 通过 --config 传给 Updater.exe。
         */
        const QString parameters =
            QStringLiteral(
                "--config \"%1\" --app-root \"%2\" --wait-pid %3")
                .arg(escapedConfigPath)
                .arg(escapedAppRoot)
                .arg(pid);

        const QString workingDirectory =
            QFileInfo(updaterExePath)
                .absolutePath();

        const HINSTANCE result =
            ShellExecuteW(
                nullptr,
                L"open",
                reinterpret_cast<LPCWSTR>(
                    updaterExePath.utf16()),
                reinterpret_cast<LPCWSTR>(
                    parameters.utf16()),
                reinterpret_cast<LPCWSTR>(
                    workingDirectory.utf16()),
                SW_SHOWNORMAL);

        return reinterpret_cast<INT_PTR>(result)
            > 32;
#else
        QStringList arguments;

        arguments
            << QStringLiteral("--config")
            << configFilePath
            << QStringLiteral("--app-root")
            << appRootPath
            << QStringLiteral("--wait-pid")
            << pid;

        return QProcess::startDetached(
            updaterExePath,
            arguments,
            QFileInfo(updaterExePath)
                .absolutePath());
#endif
    }

    int httpStatusCode(
        QNetworkReply *reply)
    {
        if (reply == nullptr)
        {
            return 0;
        }

        return reply->attribute(
            QNetworkRequest::HttpStatusCodeAttribute)
            .toInt();
    }
}

AutoUpdateChecker::AutoUpdateChecker(
    QObject *parent)
    : QObject(parent)
{
    m_network =
        new QNetworkAccessManager(this);
}

void AutoUpdateChecker::checkForUpdates(
    QWidget *parentWidget,
    const QString &currentVersion,
    const QString &updaterExePath,
    const QString &appRootPath)
{
    const QFileInfo updaterInfo(
        updaterExePath);

    const QString updaterDirectory =
        updaterInfo.absoluteDir()
            .absolutePath();

    const QString bootstrapPath =
        QDir(updaterDirectory)
            .filePath(
                QStringLiteral(
                    "updater.bootstrap.json"));

    BootstrapConfig bootstrap;
    QString bootstrapError;

    const bool bootstrapValid =
        QFileInfo::exists(bootstrapPath)
        && loadBootstrapConfig(
            bootstrapPath,
            bootstrap,
            bootstrapError);

    /*
     * 第一优先级：
     * 新版设备更新授权后保存在 LocalAppData 的 updater.json。
     */
    if (bootstrapValid)
    {
        const QString localConfigPath =
            getWritableConfigPath(
                bootstrap.softwareCode);

        UpdaterConfig localConfig;

        if (tryLoadUpdaterConfig(
                localConfigPath,
                localConfig))
        {
            checkWithConfig(
                parentWidget,
                currentVersion,
                updaterExePath,
                appRootPath,
                localConfig.serverUrl,
                localConfig.softwareCode,
                localConfig.updateToken,
                localConfig.configFilePath);

            return;
        }
    }

    /*
     * 第二优先级：兼容已经部署出去的旧版客户端。
     *
     * 如果 updater/updater.json 仍然存在并且包含旧共享 Token，
     * 继续允许它检查更新，不要求客户立刻重新授权更新。
     */
    const QString legacyConfigPath =
        QDir(updaterDirectory)
            .filePath(
                QStringLiteral("updater.json"));

    UpdaterConfig legacyConfig;

    if (tryLoadUpdaterConfig(
            legacyConfigPath,
            legacyConfig))
    {
        checkWithConfig(
            parentWidget,
            currentVersion,
            updaterExePath,
            appRootPath,
            legacyConfig.serverUrl,
            legacyConfig.softwareCode,
            legacyConfig.updateToken,
            legacyConfig.configFilePath);

        return;
    }

    /*
     * 没有可用 Token 时必须依赖 bootstrap 做首次更新授权。
     */
    if (!bootstrapValid)
    {
        QString message;

        if (!QFileInfo::exists(bootstrapPath))
        {
            message =
                QStringLiteral(
                    "找不到首次更新授权配置文件：\n")
                + bootstrapPath;
        }
        else
        {
            message =
                bootstrapError.isEmpty()
                ? QStringLiteral(
                    "updater.bootstrap.json 配置无效")
                : bootstrapError;
        }

        QMessageBox::warning(
            parentWidget,
            QStringLiteral("设备更新授权"),
            message);

        return;
    }

    const QString activationCode =
        ActivationCodeDialog::showActivationCode(
            parentWidget);

    /*
     * 用户选择取消时，不阻止业务软件正常启动。
     */
    if (activationCode.isEmpty())
    {
        return;
    }

    const QUrl activationUrl(
        bootstrap.serverUrl
        + QStringLiteral(
            "/api/client-activation/activate"));

    QNetworkRequest request(
        activationUrl);

    request.setHeader(
        QNetworkRequest::ContentTypeHeader,
        QStringLiteral("application/json"));

    QJsonObject body;

    body.insert(
        QStringLiteral("activationCode"),
        activationCode);

    body.insert(
        QStringLiteral("softwareCode"),
        bootstrap.softwareCode);

    QString deviceName =
        QHostInfo::localHostName()
            .trimmed();

    if (deviceName.isEmpty())
    {
        deviceName =
            QStringLiteral("未命名设备");
    }

    body.insert(
        QStringLiteral("deviceName"),
        deviceName);

    QNetworkReply *reply =
        m_network->post(
            request,
            QJsonDocument(body)
                .toJson(
                    QJsonDocument::Compact));

    connect(
        reply,
        &QNetworkReply::finished,
        this,
        [=]()
        {
            const QByteArray responseData =
                reply->readAll();

            const QNetworkReply::NetworkError networkError =
                reply->error();

            const int statusCode =
                httpStatusCode(reply);

            reply->deleteLater();

            const bool success =
                networkError
                    == QNetworkReply::NoError
                && statusCode >= 200
                && statusCode < 300;

            if (!success)
            {
                QString detail =
                    QString::fromUtf8(
                        responseData)
                        .trimmed();

                if (detail.isEmpty())
                {
                    detail =
                        QStringLiteral(
                            "网络错误或服务器暂时不可用");
                }

                QMessageBox::warning(
                    parentWidget,
                    QStringLiteral("设备更新授权"),
                    QStringLiteral("设备更新授权失败：\n")
                    + detail);

                return;
            }

            QJsonParseError parseError;

            const QJsonDocument document =
                QJsonDocument::fromJson(
                    responseData,
                    &parseError);

            if (parseError.error
                    != QJsonParseError::NoError
                || !document.isObject())
            {
                QMessageBox::warning(
                    parentWidget,
                    QStringLiteral("设备更新授权"),
                    QStringLiteral(
                        "设备更新授权失败：服务器返回格式无效。"));

                return;
            }

            const QJsonObject result =
                document.object();

            const QString updateToken =
                result.value(
                    QStringLiteral("updateToken"))
                    .toString()
                    .trimmed();

            if (updateToken.isEmpty())
            {
                QMessageBox::warning(
                    parentWidget,
                    QStringLiteral("设备更新授权"),
                    QStringLiteral(
                        "设备更新授权失败：服务器没有返回更新凭证。"));

                return;
            }

            const QString configPath =
                getWritableConfigPath(
                    bootstrap.softwareCode);

            QString saveError;

            if (!saveUpdaterConfig(
                    configPath,
                    bootstrap,
                    updateToken,
                    saveError))
            {
                QMessageBox::warning(
                    parentWidget,
                    QStringLiteral("设备更新授权"),
                    saveError);

                return;
            }

            const QString responseDeviceName =
                result.value(
                    QStringLiteral("deviceName"))
                    .toString()
                    .trimmed();

            QMessageBox::information(
                parentWidget,
                QStringLiteral("设备更新授权"),
                QStringLiteral(
                    "设备更新授权成功。\n\n设备：%1")
                    .arg(
                        responseDeviceName.isEmpty()
                        ? deviceName
                        : responseDeviceName));

            /*
             * 激活成功后不需要用户重启软件，
             * 直接继续本次更新检查。
             */
            checkWithConfig(
                parentWidget,
                currentVersion,
                updaterExePath,
                appRootPath,
                bootstrap.serverUrl,
                bootstrap.softwareCode,
                updateToken,
                configPath);
        });
}

void AutoUpdateChecker::checkWithConfig(
    QWidget *parentWidget,
    const QString &currentVersion,
    const QString &updaterExePath,
    const QString &appRootPath,
    const QString &serverUrl,
    const QString &softwareCode,
    const QString &updateToken,
    const QString &configFilePath)
{
    const QUrl url(
        trimBaseUrl(serverUrl)
        + QStringLiteral(
            "/api/client-updates/check"));

    QNetworkRequest request(url);

    request.setHeader(
        QNetworkRequest::ContentTypeHeader,
        QStringLiteral("application/json"));

    request.setRawHeader(
        "X-Update-Token",
        updateToken.toUtf8());

    QJsonObject body;

    body.insert(
        QStringLiteral("softwareCode"),
        softwareCode);

    body.insert(
        QStringLiteral("currentVersion"),
        currentVersion);

    QNetworkReply *reply =
        m_network->post(
            request,
            QJsonDocument(body)
                .toJson(
                    QJsonDocument::Compact));

    connect(
        reply,
        &QNetworkReply::finished,
        this,
        [=]()
        {
            const QByteArray responseData =
                reply->readAll();

            const QNetworkReply::NetworkError error =
                reply->error();

            const int statusCode =
                httpStatusCode(reply);

            reply->deleteLater();

            /*
             * 自动更新是辅助能力。
             * 网络临时失败、设备 Token 被停用等情况
             * 不阻止业务软件正常启动。
             */
            if (error != QNetworkReply::NoError
                || statusCode < 200
                || statusCode >= 300)
            {
                return;
            }

            QJsonParseError parseError;

            const QJsonDocument document =
                QJsonDocument::fromJson(
                    responseData,
                    &parseError);

            if (parseError.error
                    != QJsonParseError::NoError
                || !document.isObject())
            {
                return;
            }

            const QJsonObject result =
                document.object();

            const bool hasUpdate =
                result.value(
                    QStringLiteral("hasUpdate"))
                    .toBool(false);

            if (!hasUpdate)
            {
                return;
            }

            const QString latestVersion =
                result.value(
                    QStringLiteral("latestVersion"))
                    .toString();

            const QString releaseNotes =
                result.value(
                    QStringLiteral("releaseNotes"))
                    .toString();

            const bool forceUpdate =
                result.value(
                    QStringLiteral("forceUpdate"))
                    .toBool(false);

            QString message =
                QStringLiteral("发现新版本：%1")
                    .arg(latestVersion);

            if (!releaseNotes.trimmed().isEmpty())
            {
                message +=
                    QStringLiteral("\n\n更新说明：\n")
                    + releaseNotes;
            }

            QMessageBox box(parentWidget);

            box.setWindowTitle(
                forceUpdate
                ? QStringLiteral("必须升级")
                : QStringLiteral("发现软件更新"));

            box.setIcon(
                QMessageBox::Information);

            box.setText(message);

            QPushButton *updateButton =
                box.addButton(
                    QStringLiteral("立即更新"),
                    QMessageBox::AcceptRole);

            if (!forceUpdate)
            {
                box.addButton(
                    QStringLiteral("稍后"),
                    QMessageBox::RejectRole);
            }
            else
            {
                box.setInformativeText(
                    QStringLiteral(
                        "该版本为强制升级版本，需要完成更新后继续使用。"));
            }

            box.exec();

            if (box.clickedButton()
                != updateButton)
            {
                if (forceUpdate)
                {
                    QCoreApplication::quit();
                }

                return;
            }

            if (!QFileInfo::exists(
                    updaterExePath))
            {
                QMessageBox::warning(
                    parentWidget,
                    QStringLiteral("更新失败"),
                    QStringLiteral(
                        "找不到自动更新程序：\n")
                        + updaterExePath);

                if (forceUpdate)
                {
                    QCoreApplication::quit();
                }

                return;
            }

            const bool started =
                startUpdaterDetached(
                    updaterExePath,
                    appRootPath,
                    configFilePath);

            if (!started)
            {
                QMessageBox::warning(
                    parentWidget,
                    QStringLiteral("更新失败"),
                    QStringLiteral(
                        "无法启动自动更新程序。\n"
                        "如果 Windows 弹出了管理员权限窗口，请确认选择“是”。"));

                if (forceUpdate)
                {
                    QCoreApplication::quit();
                }

                return;
            }

            /*
             * Updater 已成功启动。
             * 主程序正常退出，让 Updater 可以替换 EXE / DLL。
             */
            QCoreApplication::quit();
        });
}
