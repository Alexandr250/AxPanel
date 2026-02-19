using Windows.Media.Control;

namespace AxPanel.SL;

public static class MediaInteractionService
{
    /// <summary>
    /// Переключает состояние воспроизведения (Play/Pause) для активной медиа-сессии.
    /// </summary>
    public static async Task TogglePlayPauseAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = manager.GetCurrentSession();

            if ( session != null )
            {
                var status = session.GetPlaybackInfo().PlaybackStatus;

                if ( status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing )
                {
                    await session.TryPauseAsync();
                }
                else
                {
                    await session.TryPlayAsync();
                }
            }
        }
        catch ( Exception ex )
        {
            System.Diagnostics.Debug.WriteLine( $"[MediaService] Error: {ex.Message}" );
        }
    }
}
