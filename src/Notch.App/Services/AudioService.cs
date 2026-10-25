using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Notch.App.Services;

/// <summary>Volume do Windows (alerta ao mudar) e nível do som para as barrinhas.</summary>
internal sealed class AudioService : IMMNotificationClient
{
    private readonly Dispatcher _dispatcher;
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _device;
    private float _lastVolume = -1;
    private bool _lastMute;

    /// <summary>(volume 0..1, mudo)</summary>
    public event Action<float, bool>? VolumeChanged;

    public AudioService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        try { _enumerator.RegisterEndpointNotificationCallback(this); } catch { }
        Attach();
    }

    private void Attach()
    {
        try
        {
            if (_device != null) _device.AudioEndpointVolume.OnVolumeNotification -= OnVolume;
            _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _lastVolume = _device.AudioEndpointVolume.MasterVolumeLevelScalar;
            _lastMute = _device.AudioEndpointVolume.Mute;
            _device.AudioEndpointVolume.OnVolumeNotification += OnVolume;
        }
        catch (Exception ex)
        {
            _device = null;
            Core.Paths.WriteLog("Áudio indisponível: " + ex.Message);
        }
    }

    private void OnVolume(AudioVolumeNotificationData data)
    {
        // ignora notificações repetidas (alguns drivers mandam várias)
        if (Math.Abs(data.MasterVolume - _lastVolume) < 0.004 && data.Muted == _lastMute) return;
        _lastVolume = data.MasterVolume;
        _lastMute = data.Muted;
        _dispatcher.BeginInvoke(() => VolumeChanged?.Invoke(data.MasterVolume, data.Muted));
    }

    public float Peak()
    {
        try { return _device?.AudioMeterInformation.MasterPeakValue ?? 0; }
        catch { return 0; }
    }

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow == DataFlow.Render && role == Role.Multimedia) _dispatcher.BeginInvoke(Attach);
    }

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
    public void OnDeviceAdded(string pwstrDeviceId) { }
    public void OnDeviceRemoved(string deviceId) { }
    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
}
