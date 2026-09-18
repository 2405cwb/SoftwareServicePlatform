#include "AutoUpdateChecker.h"

#include <QCoreApplication>
#include <QFileInfo>
#include <QJsonDocument>
#include <QJsonObject>
#include <QMessageBox>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QNetworkRequest>
#include <QProcess>
#include <QUrl>

AutoUpdateChecker::AutoUpdateChecker(QObject *parent)
    : QObject(parent)
{
    m_network = new QNetworkAccessManager(this);
}

void AutoUpdateChecker::checkForUpdates(
    QWidget *parentWidget,
    const QString &serverUrl,
    const QString &softwareCode,
    const QString &updateToken,
    const QString &currentVersion,
    const QString &updaterExePath,
    const QString &appRootPath)
{
    QString baseUrl = serverUrl.trimmed();

    while (baseUrl.endsWith('/'))
    {
        baseUrl.chop(1);
    }

    const QUrl url(
        baseUrl + "/api/client-updates/check");

    QNetworkRequest request(url);

    request.setHeader(
        QNetworkRequest::ContentTypeHeader,
        "application/json");

    /*
     * UpdateToken 不放 URL，
     * 避免出现在 QueryString / 普通访问日志。
     */
    request.setRawHeader(
        "X-Update-Token",
        updateToken.toUtf8());

    QJsonObject body;

    body.insert(
        "softwareCode",
        softwareCode);

    body.insert(
        "currentVersion",
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
             * 启动检查失败不应该阻止软件正常启动。
             *
             * 这里示例只安静返回；
             * 正式工程可以记录日志。
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
                result.value("hasUpdate")
                    .toBool(false);

            if (!hasUpdate)
            {
                return;
            }

            const QString latestVersion =
                result.value("latestVersion")
                    .toString();

            const QString releaseNotes =
                result.value("releaseNotes")
                    .toString();

            const bool forceUpdate =
                result.value("forceUpdate")
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
                /*
                 * 强制升级不能通过关闭弹窗绕过。
                 *
                 * 普通升级：
                 * 用户选择“稍后”即可继续使用。
                 *
                 * 强制升级：
                 * 用户没有点击“立即更新”，
                 * 则直接退出主程序。
                 */
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

            QStringList arguments;

            /*
             * Updater 默认从自己目录读取 updater.json。
             */
            arguments
                << "--app-root"
                << appRootPath
                << "--wait-pid"
                << QString::number(
                       QCoreApplication
                           ::applicationPid());

            const bool started =
                QProcess::startDetached(
                    updaterExePath,
                    arguments,
                    QFileInfo(
                        updaterExePath)
                        .absolutePath());

            if (!started)
            {
                QMessageBox::warning(
                    parentWidget,
                    QStringLiteral("更新失败"),
                    QStringLiteral(
                        "无法启动自动更新程序。"));

                if (forceUpdate)
                {
                    QCoreApplication::quit();
                }

                return;
            }

            /*
             * Updater 已经启动。
             *
             * 正常退出，让 Updater 可以替换 EXE / DLL。
             */
            QCoreApplication::quit();
        });
}
