#pragma once

#include <QObject>
#include <QString>

class QWidget;
class QNetworkAccessManager;

/*
 * Qt 5.8 自动更新接入示例。
 *
 * 新版流程：
 * 1. 安装包只携带非敏感的 updater.bootstrap.json；
 * 2. 首次启动如果本机没有 updater.json，就提示客户输入一次性激活码；
 * 3. 服务端为“这一台安装实例”签发独立 UpdateToken；
 * 4. 客户端自动把 updater.json 保存到 LocalAppData；
 * 5. 后续启动直接使用本机设备 Token 检查更新。
 *
 * 客户不再手工填写 UpdateToken。
 */
class AutoUpdateChecker : public QObject
{
    Q_OBJECT

public:
    explicit AutoUpdateChecker(
        QObject *parent = nullptr);

    /*
     * 建议在主窗口显示完成以后异步调用。
     *
     * currentVersion：
     * 推荐读取安装目录中的 version.txt。
     *
     * updaterExePath：
     * updater/SoftwareServicePlatform.Updater.exe
     *
     * appRootPath：
     * 当前业务软件安装目录。
     */
    void checkForUpdates(
        QWidget *parentWidget,
        const QString &currentVersion,
        const QString &updaterExePath,
        const QString &appRootPath);

private:
    /*
     * 已经取得设备 UpdateToken 后执行真正的检查更新。
     *
     * configFilePath 会在启动 Updater.exe 时通过 --config 传入，
     * 因此 Updater 不再强制从自身目录读取 updater.json。
     */
    void checkWithConfig(
        QWidget *parentWidget,
        const QString &currentVersion,
        const QString &updaterExePath,
        const QString &appRootPath,
        const QString &serverUrl,
        const QString &softwareCode,
        const QString &updateToken,
        const QString &configFilePath);

private:
    QNetworkAccessManager *m_network = nullptr;
};
