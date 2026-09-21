#include "AutoUpdateChecker.h"

#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QJsonDocument>
#include <QJsonObject>
#include <QMessageBox>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QNetworkRequest>
#include <QProcess>
#include <QPushButton>
#include <QUrl>
#include <QtGlobal>

#ifdef Q_OS_WIN
#include <windows.h>
#include <shellapi.h>
#endif

namespace
{
    struct UpdaterConfig
    {
        QString serverUrl;
        QString softwareCode;
        QString updateToken;
    };

    bool loadUpdaterConfig(
        const QString &updaterExePath,
        UpdaterConfig &config,
        QString &errorMessage)
    {
        const QFileInfo updaterInfo(
            updaterExePath);

        const QString configPath =
            updaterInfo.absoluteDir().filePath(
                QStringLiteral("updater.json"));

        QFile file(configPath);

        if (!file.open(QIODevice::ReadOnly))
        {
            errorMessage =
                QStringLiteral("找不到或无法读取自动更新配置文件：\n")
                + configPath;

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
                QStringLiteral("updater.json 格式无效：\n")
                + parseError.errorString();

            return false;
        }

        const QJsonObject object =
            document.object();

        config.serverUrl =
            object.value(
                QStringLiteral("serverUrl"))
                .toString()
                .trimmed();

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

        if (config.serverUrl.isEmpty()
            || config.softwareCode.isEmpty()
            || config.updateToken.isEmpty())
        {
            errorMessage =
                QStringLiteral(
                    "updater.json 配置不完整，必须包含：\n"
                    "serverUrl / softwareCode / updateToken");

            return false;
        }

        const QUrl serverUrl(config.serverUrl);

        if (!serverUrl.isValid()
            || (serverUrl.scheme().compare(
                    QStringLiteral("http"),
                    Qt::CaseInsensitive) != 0
                && serverUrl.scheme().compare(
                    QStringLiteral("https"),
                    Qt::CaseInsensitive) != 0))
        {
            errorMessage =
                QStringLiteral("updater.json 的 serverUrl 无效：\n")
                + config.serverUrl;

            return false;
        }

        return true;
    }

    bool startUpdaterDetached(
        const QString &updaterExePath,
        const QString &appRootPath)
    {
        const QString pid =
            QString::number(
                QCoreApplication::applicationPid());

#ifdef Q_OS_WIN
        /*
         * Updater 如果在自身 manifest 中声明 requireAdministrator，
         * Windows 需要通过 Shell 启动才能正常触发 UAC。
         *
         * 这里使用普通 "open"，不在业务代码里硬编码 runas；
         * 是否需要管理员权限由 Updater 自己的 manifest 决定。
         */
        QString escapedAppRoot =
            appRootPath;

        escapedAppRoot.replace(
            QStringLiteral("\""),
            QStringLiteral("\\\""));

        const QString parameters =
            QStringLiteral(
                "--app-root \"%1\" --wait-pid %2")
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
}

AutoUpdateChecker::AutoUpdateChecker(QObject *parent)
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
    UpdaterConfig config;
    QString configError;

    if (!loadUpdaterConfig(
            updaterExePath,
            config,
            configError))
    {
        QMessageBox::warning(
            parentWidget,
            QStringLiteral("自动更新"),
            configError);

        return;
    }

    QString baseUrl =
        config.serverUrl.trimmed();

    while (baseUrl.endsWith('/'))
    {
        baseUrl.chop(1);
    }

    const QUrl url(
        baseUrl
        + QStringLiteral(
            "/api/client-updates/check"));

    QNetworkRequest request(url);

    request.setHeader(
        QNetworkRequest::ContentTypeHeader,
        QStringLiteral("application/json"));

    request.setRawHeader(
        "X-Update-Token",
        config.updateToken.toUtf8());

    QJsonObject body;

    body.insert(
        QStringLiteral("softwareCode"),
        config.softwareCode);

    body.insert(
        QStringLiteral("currentVersion"),
        currentVersion);

    QNetworkReply *reply =
        m_network->post(
            request,
            QJsonDocument(body).toJson(
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

            reply->deleteLater();

            /*
             * 启动时检查失败不阻止业务软件正常启动。
             */
            if (error != QNetworkReply::NoError)
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
                QStringLiteral("发现新版本 %1")
                    .arg(latestVersion);

            if (!releaseNotes.trimmed().isEmpty())
            {
                message +=
                    QStringLiteral("\n\n更新说明：\n")
                    + releaseNotes;
            }

            QMessageBox box(parentWidget);

            box.setWindowTitle(
                QStringLiteral("软件更新"));

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
                    appRootPath);

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
             * Updater 已经启动。
             * 正常退出，让 Updater 可以替换 EXE / DLL。
             */
            QCoreApplication::quit();
        });
}
