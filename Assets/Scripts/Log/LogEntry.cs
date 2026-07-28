// One line in the player log. Text already carries IconMarkup sprite tags, so
// entries render exactly like the messages they replaced, with no reformatting.
public readonly struct LogEntry
{
    public readonly int Day;
    public readonly string Text;

    public LogEntry(int day, string text)
    {
        Day = day;
        Text = text;
    }
}
