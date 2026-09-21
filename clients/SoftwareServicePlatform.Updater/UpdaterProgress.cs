namespace SoftwareServicePlatform.Updater
{
    public sealed class UpdaterProgress
    {
        public string Message { get; set; } =
            string.Empty;

        public string CurrentFile { get; set; } =
            string.Empty;

        public int Current { get; set; }

        public int Total { get; set; }

        public bool IsIndeterminate { get; set; }


        public int Percent
        {
            get
            {
                if (Total <= 0)
                {
                    return 0;
                }

                return Math.Clamp(
                    (int)(
                        Current * 100.0 / Total
                    ),
                    0,
                    100
                );
            }
        }
    }
}