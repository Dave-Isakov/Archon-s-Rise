public class SkillCommand : ICommands
{
    SkillToken _token;
    SkillEvent _skillAction;

    public SkillCommand(SkillEvent skillEvent, SkillToken token)
    {
        _skillAction = skillEvent;
        _token = token;
    }

    public void Execute()
    {
        _skillAction.Raise(_token);
    }

    public void Undo()
    {
        _skillAction.Raise(_token);
    }
}
