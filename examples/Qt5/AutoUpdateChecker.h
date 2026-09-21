#pragma once

#include <QObject>
#include <QString>

class QWidget;
class QNetworkAccessManager;

/*
 * Qt 5.8 自动更新接入示例。
 *
 * serverUrl / softwareCode / updateToken
 * 不再由业务代码传入，统一读取：
 *
 * updater/updater.json
 *
 * 主程序只负责：
 * 1. 读取 updater.json；
 * 2. 调用 /api/client-updates/check；
 * 3. 提示用户；
 * 4. 启动独立 Updater.exe；
 * 5. 正常退出。
 *
 * 真正的文件下载、SHA256、备份、替换、回滚、UAC、重启
 * 全部由 SoftwareServicePlatform.Updater.exe 完成。
 */
class AutoUpdateChecker : public QObject
{
    Q_OBJECT

public:
    explicit AutoUpdateChecker(QObject *parent = nullptr);

    /*
     * 建议在主窗口显示完成以后异步调用。
     *
     * currentVersion：
     * 推荐读取安装目录的 version.txt。
     */
    void checkForUpdates(
        QWidget *parentWidget,
        const QString &currentVersion,
        const QString &updaterExePath,
        const QString &appRootPath);

private:
    QNetworkAccessManager *m_network = nullptr;
};
