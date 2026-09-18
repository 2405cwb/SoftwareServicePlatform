#pragma once

#include <QObject>
#include <QString>

class QWidget;
class QNetworkAccessManager;

/*
 * Qt 5.8 自动更新接入示例。
 *
 * 主程序只负责：
 *
 * 1. 调用 /api/client-updates/check；
 * 2. 提示用户是否立即更新；
 * 3. 启动独立 Updater.exe；
 * 4. 正常退出主程序。
 *
 * 真正的文件下载、SHA256、备份、替换、回滚、重启
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
     * 可以传 QCoreApplication::applicationVersion()，
     * 也可以读取安装目录下的 version.txt。
     */
    void checkForUpdates(
        QWidget *parentWidget,
        const QString &serverUrl,
        const QString &softwareCode,
        const QString &updateToken,
        const QString &currentVersion,
        const QString &updaterExePath,
        const QString &appRootPath);

private:
    QNetworkAccessManager *m_network = nullptr;
};
