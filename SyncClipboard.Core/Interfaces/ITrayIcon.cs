﻿namespace SyncClipboard.Core.Interfaces
{
    public interface ITrayIcon
    {
        void Create();
        event Action MainWindowWakedUp;
    }
}