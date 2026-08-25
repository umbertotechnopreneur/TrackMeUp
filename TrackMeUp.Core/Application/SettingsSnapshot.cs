namespace TrackMeUp.Application;

/// <summary>Holds the immutable settings value currently owned by the runtime.</summary>
public sealed class SettingsSnapshot
{
    private AppSettings _value;

    /// <summary>Initializes a settings snapshot with the validated runtime value.</summary>
    public SettingsSnapshot(AppSettings value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets the current immutable settings value without accessing persistence.</summary>
    public AppSettings Value => Volatile.Read(ref _value);

    /// <summary>Replaces the value after the corresponding persistence operation succeeds.</summary>
    public void Replace(AppSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Volatile.Write(ref _value, value);
    }
}
