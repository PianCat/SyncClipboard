using System;
using System.Threading;
using System.Threading.Tasks;
using SyncClipboard.Module;
using SyncClipboard.Utility;
using SyncClipboard.Utility.Notification;
#nullable enable

namespace SyncClipboard.Service
{
    public class UploadService : Service
    {
        public event ProgramEvent.ProgramEventHandler? PushStarted;
        public event ProgramEvent.ProgramEventHandler? PushStopped;

        private const string SERVICE_NAME = "⬆⬆";
        private const string LOG_TAG = "PUSH";
        private bool _downServiceChangingLocal = false;

        protected override void StartService()
        {
            Global.Notifyer.SetStatusString(SERVICE_NAME, "Running.");
        }

        protected override void StopSerivce()
        {
            Global.Notifyer.SetStatusString(SERVICE_NAME, "Stopped.");
            StopPreviousAndGetNewToken();
        }

        public override void RegistEvent()
        {
            var pushStartedEvent = new ProgramEvent(
                (handler) => PushStarted += handler,
                (handler) => PushStarted -= handler
            );
            Event.RegistEvent(SyncService.PUSH_START_ENENT_NAME, pushStartedEvent);

            var pushStoppedEvent = new ProgramEvent(
                (handler) => PushStopped += handler,
                (handler) => PushStopped -= handler
            );
            Event.RegistEvent(SyncService.PUSH_STOP_ENENT_NAME, pushStoppedEvent);
        }

        public override void RegistEventHandler()
        {
            Event.RegistEventHandler(ClipboardService.CLIPBOARD_CHANGED_EVENT_NAME, ClipBoardChangedHandler);
            Event.RegistEventHandler(SyncService.PULL_START_ENENT_NAME, PullStartedHandler);
            Event.RegistEventHandler(SyncService.PULL_STOP_ENENT_NAME, PullStoppedHandler);
        }

        public override void UnRegistEventHandler()
        {
            Event.UnRegistEventHandler(ClipboardService.CLIPBOARD_CHANGED_EVENT_NAME, ClipBoardChangedHandler);
            Event.UnRegistEventHandler(SyncService.PULL_START_ENENT_NAME, PullStartedHandler);
            Event.UnRegistEventHandler(SyncService.PULL_STOP_ENENT_NAME, PullStoppedHandler);
        }

        public void PullStartedHandler()
        {
            Log.Write("_isChangingLocal set to TRUE");
            _downServiceChangingLocal = true;
        }

        public void PullStoppedHandler()
        {
            Log.Write("_isChangingLocal set to FALSE");
            _downServiceChangingLocal = false;
        }

        private CancellationTokenSource? _cancelSource;
        private readonly object _cancelSourceLocker = new();
        private uint sessionNumber = 0;

        private void ClipBoardChangedHandler()
        {
            if (!UserConfig.Config.SyncService.PushSwitchOn || _downServiceChangingLocal)
            {
                return;
            }

            ProcessUploadQueue();
        }

        private CancellationToken StopPreviousAndGetNewToken()
        {
            lock (_cancelSourceLocker)
            {
                if (_cancelSource?.Token.CanBeCanceled ?? false)
                {
                    _cancelSource.Cancel();
                }
                _cancelSource = new();
                return _cancelSource.Token;
            }
        }

        private void SetWorkingStartStatus()
        {
            Interlocked.Increment(ref sessionNumber);
            SetUploadingIcon();
            Global.Notifyer.SetStatusString(SERVICE_NAME, "Uploading.");
            PushStarted?.Invoke();
        }

        private void SetWorkingEndStatus()
        {
            Interlocked.Decrement(ref sessionNumber);
            if (sessionNumber == 0)
            {
                StopUploadingIcon();
                Global.Notifyer.SetStatusString(SERVICE_NAME, "Running.", false);
                PushStopped?.Invoke();
            }
        }

        private async void ProcessUploadQueue()
        {
            SetWorkingStartStatus();
            CancellationToken cancelToken = StopPreviousAndGetNewToken();
            try
            {
                await UploadClipboard(cancelToken);
            }
            catch (OperationCanceledException)
            {
                Log.Write("Upload", "Upload Canceled");
            }
            SetWorkingEndStatus();
        }

        private static async Task UploadClipboard(CancellationToken cancelToken)
        {
            var currentProfile = ProfileFactory.CreateFromLocal();

            if (currentProfile.GetProfileType() == ProfileType.ClipboardType.Unknown)
            {
                Log.Write("Local profile type is Unkown, stop upload.");
                return;
            }

            await UploadLoop(currentProfile, cancelToken);
        }

        private static async Task UploadLoop(Profile profile, CancellationToken cancelToken)
        {
            string errMessage = "";
            for (int i = 0; i < UserConfig.Config.Program.RetryTimes; i++)
            {
                try
                {
                    SyncService.remoteProfilemutex.WaitOne();
                    var remoteProfile = await ProfileFactory.CreateFromRemote(Global.WebDav, cancelToken);
                    if (!await Profile.Same(remoteProfile, profile, cancelToken))
                    {
                        await profile.UploadProfileAsync(Global.WebDav, cancelToken);
                    }
                    Log.Write(LOG_TAG, "remote is same as local, won't push");
                    return;
                }
                catch (TaskCanceledException)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    Global.Notifyer.SetStatusString(SERVICE_NAME, $"失败，正在第{i + 1}次尝试，错误原因：请求超时", true);
                    errMessage = "连接超时";
                }
                catch (Exception ex)
                {
                    errMessage = ex.Message;
                    Global.Notifyer.SetStatusString(SERVICE_NAME, $"失败，正在第{i + 1}次尝试，错误原因：{errMessage}", true);
                }
                finally
                {
                    SyncService.remoteProfilemutex.ReleaseMutex();
                }

                await Task.Delay(TimeSpan.FromSeconds(UserConfig.Config.Program.IntervalTime), cancelToken);
            }
            Toast.SendText("上传失败：" + profile.ToolTip(), errMessage);
        }

        private static void SetUploadingIcon()
        {
            System.Drawing.Icon[] icon =
            {
                Properties.Resources.upload001, Properties.Resources.upload002, Properties.Resources.upload003,
                Properties.Resources.upload004, Properties.Resources.upload005, Properties.Resources.upload006,
                Properties.Resources.upload007, Properties.Resources.upload008, Properties.Resources.upload009,
                Properties.Resources.upload010, Properties.Resources.upload011, Properties.Resources.upload012,
                Properties.Resources.upload013, Properties.Resources.upload014, Properties.Resources.upload015,
                Properties.Resources.upload016, Properties.Resources.upload017,
            };

            Global.Notifyer.SetDynamicNotifyIcon(icon, 150);
        }

        private static void StopUploadingIcon()
        {
            Global.Notifyer.StopDynamicNotifyIcon();
        }
    }
}