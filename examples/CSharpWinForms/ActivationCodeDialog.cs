using System;
using System.Drawing;
using System.Windows.Forms;

namespace YourApplication
{
    /// <summary>
    /// 第一次安装时输入一次性更新激活码的小窗口。
    ///
    /// 不要求客户填写 serverUrl / softwareCode / UpdateToken，
    /// 客户只需要从软件服务平台“我的软件”页面复制一次性更新激活码。
    /// </summary>
    internal sealed class ActivationCodeDialog : Form
    {
        private readonly TextBox _txtCode;

        public string ActivationCode
        {
            get { return _txtCode.Text.Trim(); }
        }

        public ActivationCodeDialog()
        {
            Text = "设备更新授权";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(430, 190);

            var lblTitle = new Label
            {
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Location = new Point(24, 22),
                Text = "请输入一次性更新激活码"
            };

            var lblTip = new Label
            {
                AutoSize = false,
                Location = new Point(24, 52),
                Size = new Size(380, 42),
                Text = "请登录软件服务平台，在“我的软件”中点击“获取更新激活码”。\r\n此授权只用于自动更新，不影响软件正常使用。"
            };

            _txtCode = new TextBox
            {
                Location = new Point(24, 102),
                Size = new Size(380, 27),
                CharacterCasing = CharacterCasing.Upper,
                Font = new Font("Consolas", 11F)
            };

            var btnActivate = new Button
            {
                Text = "确认授权",
                DialogResult = DialogResult.OK,
                Location = new Point(238, 145),
                Size = new Size(78, 30)
            };

            var btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(326, 145),
                Size = new Size(78, 30)
            };

            AcceptButton = btnActivate;
            CancelButton = btnCancel;

            Controls.Add(lblTitle);
            Controls.Add(lblTip);
            Controls.Add(_txtCode);
            Controls.Add(btnActivate);
            Controls.Add(btnCancel);
        }

        public static string ShowActivationCode(IWin32Window owner)
        {
            using (var dialog = new ActivationCodeDialog())
            {
                if (dialog.ShowDialog(owner) != DialogResult.OK)
                {
                    return null;
                }

                return dialog.ActivationCode;
            }
        }
    }
}
