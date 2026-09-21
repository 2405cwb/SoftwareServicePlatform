namespace SoftwareServicePlatform.Updater
{
    /// <summary>
    /// 软件自动更新窗口。
    ///
    /// 设计目标：
    /// 1. 不依赖 Designer，直接代码创建界面；
    /// 2. 支持 Windows DPI 缩放，避免 125% / 150% 下文字显示不全；
    /// 3. 清晰区分“标题、版本、当前阶段、进度、当前文件、提示”；
    /// 4. 更新过程中阻止误关闭，完成或失败后允许正常关闭。
    /// </summary>
    public sealed class UpdaterForm : Form
    {
        private readonly UpdaterConfig
            _config;

        private readonly string
            _appRoot;

        private readonly int?
            _waitPid;


        private readonly Label
            _titleLabel;

        private readonly Label
            _versionLabel;

        private readonly Label
            _statusLabel;

        private readonly Label
            _progressTextLabel;

        private readonly ProgressBar
            _progressBar;

        private readonly Label
            _fileCaptionLabel;

        private readonly Label
            _fileLabel;

        private readonly Label
            _tipLabel;


        private bool
            _isUpdating = true;

        private bool
            _allowClose;


        public int ExitCode
        {
            get;
            private set;
        }


        public UpdaterForm(
            UpdaterConfig config,
            string appRoot,
            int? waitPid)
        {
            _config =
                config;

            _appRoot =
                appRoot;

            _waitPid =
                waitPid;


            /*
             * ==========================================
             * 窗口基础设置
             * ==========================================
             */
            Text =
                "软件更新";

            StartPosition =
                FormStartPosition.CenterScreen;

            FormBorderStyle =
                FormBorderStyle.FixedSingle;

            MaximizeBox =
                false;

            MinimizeBox =
                true;

            ShowIcon =
                true;

            /*
             * 重点：
             * 不再使用死板的小窗口尺寸。
             *
             * ClientSize 比 Width / Height 更准确，
             * 不包含标题栏和边框。
             */
            ClientSize =
                new Size(
                    680,
                    390
                );

            MinimumSize =
                new Size(
                    696,
                    429
                );

            /*
             * DPI 缩放。
             *
             * Windows 设置 125%、150% 时，
             * WinForms 会自动根据 DPI 调整，
             * 减少“文字只显示一半”的问题。
             */
            AutoScaleMode =
                AutoScaleMode.Dpi;

            Font =
                new Font(
                    "Microsoft YaHei UI",
                    9F,
                    FontStyle.Regular,
                    GraphicsUnit.Point
                );

            BackColor =
                Color.White;


            /*
             * ==========================================
             * 最外层布局
             * ==========================================
             *
             * 使用 TableLayoutPanel + Dock，
             * 不再大量依赖 Left / Top 固定坐标。
             */
            var root =
                new TableLayoutPanel
                {
                    Dock =
                        DockStyle.Fill,

                    BackColor =
                        Color.White,

                    Padding =
                        new Padding(
                            32,
                            26,
                            32,
                            24
                        ),

                    ColumnCount =
                        1,

                    RowCount =
                        8
                };


            root.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100F
                )
            );


            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    52F
                )
            );

            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    32F
                )
            );

            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    52F
                )
            );

            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    34F
                )
            );

            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    42F
                )
            );

            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    26F
                )
            );

            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100F
                )
            );

            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    44F
                )
            );


            /*
             * ==========================================
             * 主标题
             * ==========================================
             */
            _titleLabel =
                new Label
                {
                    Text =
                        "正在准备软件更新",

                    Dock =
                        DockStyle.Fill,

                    AutoSize =
                        false,

                    TextAlign =
                        ContentAlignment.MiddleLeft,

                    Font =
                        new Font(
                            "Microsoft YaHei UI",
                            16F,
                            FontStyle.Bold,
                            GraphicsUnit.Point
                        ),

                    ForeColor =
                        Color.FromArgb(
                            32,
                            33,
                            36
                        )
                };


            /*
             * 当前版本 / 目标版本。
             *
             * 例如：
             * 当前版本 2.2.6.3    →    目标版本 2.2.6.4
             */
            _versionLabel =
                new Label
                {
                    Text =
                        "正在读取版本信息...",

                    Dock =
                        DockStyle.Fill,

                    AutoSize =
                        false,

                    TextAlign =
                        ContentAlignment.MiddleLeft,

                    Font =
                        new Font(
                            "Microsoft YaHei UI",
                            9.5F,
                            FontStyle.Regular,
                            GraphicsUnit.Point
                        ),

                    ForeColor =
                        Color.FromArgb(
                            95,
                            99,
                            104
                        )
                };


            /*
             * ==========================================
             * 当前状态
             * ==========================================
             */
            _statusLabel =
                new Label
                {
                    Text =
                        "正在初始化更新组件...",

                    Dock =
                        DockStyle.Fill,

                    AutoSize =
                        false,

                    TextAlign =
                        ContentAlignment.BottomLeft,

                    Font =
                        new Font(
                            "Microsoft YaHei UI",
                            10.5F,
                            FontStyle.Regular,
                            GraphicsUnit.Point
                        ),

                    ForeColor =
                        Color.FromArgb(
                            45,
                            45,
                            45
                        )
                };


            /*
             * ==========================================
             * 进度区域
             * ==========================================
             */
            var progressPanel =
                new TableLayoutPanel
                {
                    Dock =
                        DockStyle.Fill,

                    ColumnCount =
                        2,

                    RowCount =
                        1,

                    Margin =
                        new Padding(
                            0
                        )
                };


            progressPanel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100F
                )
            );

            progressPanel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    105F
                )
            );


            _progressBar =
                new ProgressBar
                {
                    Dock =
                        DockStyle.Fill,

                    Minimum =
                        0,

                    Maximum =
                        100,

                    Value =
                        0,

                    Style =
                        ProgressBarStyle.Blocks,

                    Margin =
                        new Padding(
                            0,
                            5,
                            12,
                            5
                        )
                };


            _progressTextLabel =
                new Label
                {
                    Text =
                        "0%",

                    Dock =
                        DockStyle.Fill,

                    AutoSize =
                        false,

                    TextAlign =
                        ContentAlignment.MiddleRight,

                    Font =
                        new Font(
                            "Microsoft YaHei UI",
                            9.5F,
                            FontStyle.Regular,
                            GraphicsUnit.Point
                        ),

                    ForeColor =
                        Color.FromArgb(
                            75,
                            75,
                            75
                        )
                };


            progressPanel.Controls.Add(
                _progressBar,
                0,
                0
            );

            progressPanel.Controls.Add(
                _progressTextLabel,
                1,
                0
            );


            /*
             * ==========================================
             * 当前文件
             * ==========================================
             */
            _fileCaptionLabel =
                new Label
                {
                    Text =
                        "当前文件",

                    Dock =
                        DockStyle.Fill,

                    AutoSize =
                        false,

                    TextAlign =
                        ContentAlignment.BottomLeft,

                    Font =
                        new Font(
                            "Microsoft YaHei UI",
                            9F,
                            FontStyle.Regular,
                            GraphicsUnit.Point
                        ),

                    ForeColor =
                        Color.FromArgb(
                            120,
                            120,
                            120
                        )
                };


            _fileLabel =
                new Label
                {
                    Text =
                        "等待开始...",

                    Dock =
                        DockStyle.Fill,

                    AutoSize =
                        false,

                    AutoEllipsis =
                        true,

                    TextAlign =
                        ContentAlignment.TopLeft,

                    Font =
                        new Font(
                            "Microsoft YaHei UI",
                            9.5F,
                            FontStyle.Regular,
                            GraphicsUnit.Point
                        ),

                    ForeColor =
                        Color.FromArgb(
                            60,
                            60,
                            60
                        ),

                    Padding =
                        new Padding(
                            0,
                            4,
                            0,
                            0
                        )
                };


            /*
             * ==========================================
             * 底部提示区
             * ==========================================
             */
            var separator =
                new Panel
                {
                    Dock =
                        DockStyle.Top,

                    Height =
                        1,

                    BackColor =
                        Color.FromArgb(
                            232,
                            234,
                            237
                        )
                };


            _tipLabel =
                new Label
                {
                    Text =
                        "更新过程中请勿关闭电脑或强制结束更新程序。",

                    Dock =
                        DockStyle.Fill,

                    AutoSize =
                        false,

                    TextAlign =
                        ContentAlignment.MiddleLeft,

                    Font =
                        new Font(
                            "Microsoft YaHei UI",
                            8.8F,
                            FontStyle.Regular,
                            GraphicsUnit.Point
                        ),

                    ForeColor =
                        Color.FromArgb(
                            115,
                            115,
                            115
                        ),

                    Padding =
                        new Padding(
                            0,
                            7,
                            0,
                            0
                        )
                };


            var bottomPanel =
                new Panel
                {
                    Dock =
                        DockStyle.Fill,

                    Margin =
                        new Padding(
                            0
                        )
                };


            bottomPanel.Controls.Add(
                _tipLabel
            );

            bottomPanel.Controls.Add(
                separator
            );


            /*
             * ==========================================
             * 放入根布局
             * ==========================================
             */
            root.Controls.Add(
                _titleLabel,
                0,
                0
            );

            root.Controls.Add(
                _versionLabel,
                0,
                1
            );

            root.Controls.Add(
                _statusLabel,
                0,
                2
            );

            root.Controls.Add(
                progressPanel,
                0,
                3
            );

            root.Controls.Add(
                _fileCaptionLabel,
                0,
                4
            );

            root.Controls.Add(
                _fileLabel,
                0,
                5
            );

            /*
             * 第 6 行故意留白，
             * 让主体和底部提示之间有呼吸感。
             */
            root.Controls.Add(
                bottomPanel,
                0,
                7
            );


            Controls.Add(
                root
            );


            /*
             * ==========================================
             * 事件
             * ==========================================
             */
            Shown +=
                UpdaterForm_Shown;

            FormClosing +=
                UpdaterForm_FormClosing;
        }


        /// <summary>
        /// 窗口首次显示后真正开始执行更新。
        ///
        /// 之所以放在 Shown 中，
        /// 是为了确保用户先看到窗口，
        /// 再开始 SHA256 / 网络请求等耗时操作。
        /// </summary>
        private async void
            UpdaterForm_Shown(
                object? sender,
                EventArgs e)
        {
            try
            {
                var progress =
                    new Progress<
                        UpdaterProgress>(
                            UpdateProgress
                        );


                var engine =
                    new UpdaterEngine(
                        _config,
                        _appRoot,
                        progress
                    );


                /*
                 * ==========================================
                 * 读取当前版本
                 * ==========================================
                 */
                var currentVersion =
                    engine
                        .ReadCurrentVersion();


                _versionLabel.Text =
                    $"当前版本：{currentVersion}";

                _statusLabel.Text =
                    "正在检查新版本...";

                SetIndeterminateProgress();


                /*
                 * ==========================================
                 * 请求平台检查更新
                 * ==========================================
                 */
                var update =
                    await engine
                        .CheckAsync(
                            currentVersion
                        );


                /*
                 * 已经是最新版本。
                 */
                if (!update.HasUpdate)
                {
                    _isUpdating =
                        false;

                    _titleLabel.Text =
                        "当前已经是最新版本";

                    _versionLabel.Text =
                        $"当前版本：{currentVersion}";

                    _statusLabel.Text =
                        "无需安装更新";

                    _fileCaptionLabel.Text =
                        "状态";

                    _fileLabel.Text =
                        "您的软件已经是最新版本。";

                    SetCompletedProgress();


                    await Task.Delay(
                        900
                    );


                    ExitCode =
                        0;

                    CloseSafely();

                    return;
                }


                /*
                 * 找到目标版本以后，
                 * 标题和版本关系一次性展示清楚。
                 */
                _titleLabel.Text =
                    "正在安装软件更新";

                _versionLabel.Text =
                    $"当前版本：{currentVersion}    →    目标版本：{update.LatestVersion}";

                _statusLabel.Text =
                    "正在准备更新文件...";


                /*
                 * ==========================================
                 * 执行真正更新
                 * ==========================================
                 *
                 * 后续检查、下载、安装进度，
                 * 都由 UpdaterEngine 通过 IProgress 回调。
                 */
                await engine.ApplyAsync(
                    update,
                    _waitPid
                );


                /*
                 * ==========================================
                 * 更新成功
                 * ==========================================
                 */
                _isUpdating =
                    false;

                _titleLabel.Text =
                    "更新完成";

                _versionLabel.Text =
                    $"{currentVersion}    →    {update.LatestVersion}";

                _statusLabel.Text =
                    "软件已成功更新，正在重新启动...";

                _fileCaptionLabel.Text =
                    "状态";

                _fileLabel.Text =
                    "所有更新文件均已安装完成。";

                SetCompletedProgress();


                await Task.Delay(
                    900
                );


                ExitCode =
                    0;

                CloseSafely();
            }
            catch (Exception ex)
            {
                /*
                 * ==========================================
                 * 更新失败
                 * ==========================================
                 *
                 * UpdaterEngine 内部已经负责：
                 *
                 * 日志
                 * 回滚
                 * report/complete 失败上报
                 *
                 * 窗口这里只负责向用户清晰展示错误。
                 */
                _isUpdating =
                    false;

                ExitCode =
                    1;


                _titleLabel.Text =
                    "更新失败";

                _statusLabel.Text =
                    "软件更新未能完成";

                _fileCaptionLabel.Text =
                    "错误信息";

                _fileLabel.Text =
                    ex.Message;

                _progressBar.Style =
                    ProgressBarStyle.Blocks;

                _progressBar.Value =
                    0;

                _progressTextLabel.Text =
                    "失败";


                MessageBox.Show(
                    this,
                    "自动更新失败。\r\n\r\n"
                    +
                    ex.Message
                    +
                    "\r\n\r\n"
                    +
                    "详细信息请查看 updater.log。",
                    "软件自动更新失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );


                CloseSafely();
            }
        }


        /// <summary>
        /// 接收 UpdaterEngine 上报的进度。
        ///
        /// Progress&lt;T&gt; 会把回调切回 WinForms UI 线程，
        /// 因此这里可以直接更新控件。
        /// </summary>
        private void UpdateProgress(
            UpdaterProgress progress)
        {
            /*
             * 当前阶段。
             *
             * 例如：
             *
             * 正在检查本地文件...
             * 正在下载更新文件...
             * 正在安装更新...
             */
            if (
                !string.IsNullOrWhiteSpace(
                    progress.Message)
            )
            {
                _statusLabel.Text =
                    progress.Message;
            }


            /*
             * 当前处理文件。
             */
            if (
                string.IsNullOrWhiteSpace(
                    progress.CurrentFile)
            )
            {
                _fileLabel.Text =
                    "正在处理...";
            }
            else
            {
                _fileLabel.Text =
                    progress.CurrentFile;
            }


            /*
             * 无法准确计算进度时，
             * 使用 Windows 原生 Marquee 动画。
             */
            if (
                progress.IsIndeterminate
            )
            {
                SetIndeterminateProgress();

                return;
            }


            _progressBar.Style =
                ProgressBarStyle.Blocks;


            var percent =
                progress.Percent;


            _progressBar.Value =
                Math.Clamp(
                    percent,
                    0,
                    100
                );


            /*
             * 同时显示：
             *
             * 8 / 14    57%
             *
             * 比单独显示 8/14 更直观。
             */
            if (
                progress.Total > 0
            )
            {
                _progressTextLabel.Text =
                    $"{progress.Current} / {progress.Total}    {percent}%";
            }
            else
            {
                _progressTextLabel.Text =
                    $"{percent}%";
            }
        }


        /// <summary>
        /// 设置为“不确定进度”模式。
        ///
        /// 例如：
        ///
        /// 检查更新
        /// 等待主程序退出
        /// 下载完整安装包
        /// </summary>
        private void SetIndeterminateProgress()
        {
            _progressBar.Style =
                ProgressBarStyle.Marquee;

            _progressBar.MarqueeAnimationSpeed =
                25;

            _progressTextLabel.Text =
                "处理中";
        }


        /// <summary>
        /// 设置 100% 完成状态。
        /// </summary>
        private void SetCompletedProgress()
        {
            _progressBar.Style =
                ProgressBarStyle.Blocks;

            _progressBar.Value =
                100;

            _progressTextLabel.Text =
                "100%";
        }


        /// <summary>
        /// 程序自己完成业务以后才允许关闭窗口。
        /// </summary>
        private void CloseSafely()
        {
            _allowClose =
                true;

            Close();
        }


        /// <summary>
        /// 防止用户在更新过程中误点右上角 X。
        ///
        /// 这里不直接禁用整个标题栏，
        /// 保留最小化能力，用户体验会比 ControlBox=false 更自然。
        /// </summary>
        private void UpdaterForm_FormClosing(
            object? sender,
            FormClosingEventArgs e)
        {
            if (
                !_allowClose
                &&
                _isUpdating
                &&
                e.CloseReason
                ==
                CloseReason.UserClosing
            )
            {
                e.Cancel =
                    true;

                MessageBox.Show(
                    this,
                    "软件正在更新，请等待更新完成后再关闭窗口。",
                    "软件更新",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }
    }
}
