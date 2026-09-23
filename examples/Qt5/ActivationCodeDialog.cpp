#include "ActivationCodeDialog.h"

#include <QDialogButtonBox>
#include <QFont>
#include <QLabel>
#include <QLineEdit>
#include <QPushButton>
#include <QVBoxLayout>

ActivationCodeDialog::ActivationCodeDialog(
    QWidget *parent)
    : QDialog(parent)
{
    setWindowTitle(
        QStringLiteral("设备更新授权"));

    setModal(true);
    setMinimumWidth(460);

    /*
     * 去掉 Windows 对话框右上角的“？”帮助按钮，
     * 让首次激活窗口更简洁。
     */
    setWindowFlags(
        windowFlags()
        & ~Qt::WindowContextHelpButtonHint);

    QVBoxLayout *layout =
        new QVBoxLayout(this);

    layout->setContentsMargins(
        24,
        22,
        24,
        20);

    layout->setSpacing(12);

    QLabel *titleLabel =
        new QLabel(
            QStringLiteral("请输入一次性更新激活码"),
            this);

    QFont titleFont =
        titleLabel->font();

    titleFont.setBold(true);
    titleFont.setPointSizeF(
        titleFont.pointSizeF() + 1.5);

    titleLabel->setFont(titleFont);

    QLabel *tipLabel =
        new QLabel(
            QStringLiteral(
                "请登录软件服务平台，在“我的软件”中点击“获取更新激活码”。\n"
                "此授权只用于自动更新，不影响软件正常使用。"),
            this);

    tipLabel->setWordWrap(true);

    m_codeEdit =
        new QLineEdit(this);

    m_codeEdit->setPlaceholderText(
        QStringLiteral("例如：ABCD-EFGH-JKLM"));

    m_codeEdit->setClearButtonEnabled(true);

    QFont codeFont(
        QStringLiteral("Consolas"));

    codeFont.setPointSize(11);
    m_codeEdit->setFont(codeFont);

    /*
     * 更新激活码为了便于客户人工输入，界面统一显示为大写。
     * 服务端本身也会标准化更新激活码，因此这里只是改善可读性。
     */
    connect(
        m_codeEdit,
        &QLineEdit::textChanged,
        this,
        [this](const QString &text)
        {
            const QString upper =
                text.toUpper();

            if (upper == text)
            {
                return;
            }

            const int cursorPosition =
                m_codeEdit->cursorPosition();

            m_codeEdit->setText(upper);
            m_codeEdit->setCursorPosition(
                cursorPosition);
        });

    QDialogButtonBox *buttons =
        new QDialogButtonBox(
            QDialogButtonBox::Ok
            | QDialogButtonBox::Cancel,
            this);

    buttons->button(
        QDialogButtonBox::Ok)
        ->setText(
            QStringLiteral("确认授权"));

    buttons->button(
        QDialogButtonBox::Cancel)
        ->setText(
            QStringLiteral("取消"));

    connect(
        buttons,
        &QDialogButtonBox::accepted,
        this,
        &QDialog::accept);

    connect(
        buttons,
        &QDialogButtonBox::rejected,
        this,
        &QDialog::reject);

    layout->addWidget(titleLabel);
    layout->addWidget(tipLabel);
    layout->addSpacing(4);
    layout->addWidget(m_codeEdit);
    layout->addSpacing(6);
    layout->addWidget(buttons);

    m_codeEdit->setFocus();
}

QString ActivationCodeDialog::activationCode() const
{
    return m_codeEdit == nullptr
        ? QString()
        : m_codeEdit->text().trimmed();
}

QString ActivationCodeDialog::showActivationCode(
    QWidget *parent)
{
    ActivationCodeDialog dialog(parent);

    if (dialog.exec()
        != QDialog::Accepted)
    {
        return QString();
    }

    return dialog.activationCode();
}
