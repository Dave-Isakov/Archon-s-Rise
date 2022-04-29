using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayManager
{
    private Stack<ICommands> commandManager;

    public PlayManager()
    {
        commandManager = new Stack<ICommands>();
    }

    public void AddCommand(ICommands newCommand)
    {
        newCommand.Execute();
        commandManager.Push(newCommand);
        GetStack();
    }

    public void UndoCommand()
    {
        if(commandManager.Count > 0)
        {
            ICommands latestCommand = commandManager.Pop();
            latestCommand.Undo();
            GetStack();
        }
    }

    public void GetStack()
    {
        foreach (var ICommands in commandManager)
            Debug.Log(ICommands);
    }

    public int GetStackCount()
    {
        return commandManager.Count;
    }

    public void ClearStack()
    {
        commandManager.Clear();
    }
}
