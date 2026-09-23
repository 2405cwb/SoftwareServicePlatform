#pragma once

#include <QDialog>
#include <QString>

class QLineEdit;
class QWidget;

/*
 * 软件首次激活窗口。
 *
 * 客户不需要填写：
 * serverUrl / softwareCode / UpdateToken。
 *
 * 客户只需要登录软件服务平台，
 * 在“我的软件”页面获取一次性激活码并输入一次。
 */
class ActivationCodeDialog : public QDialog
{
public:
    explicit ActivationCodeDialog(
        QWidget *parent = nullptr);

    /*
     * 返回用户输入并去除首尾空格后的激活码。
     */
    QString activationCode() const;

    /*
     * 便捷调用。
     *
     * 用户取消时返回空字符串。
     */
    static QString showActivationCode(
        QWidget *parent);

private:
    QLineEdit *m_codeEdit = nullptr;
};
