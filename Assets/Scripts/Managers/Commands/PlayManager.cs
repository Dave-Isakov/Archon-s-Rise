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

    public bool IsEmpty => commandManager.Count == 0;

    public int GetStackCount()
    {
        return commandManager.Count;
    }

    public void ClearStack()
    {
        // These plays can no longer be undone; commit each one (card plays move to discard).
        foreach (var command in commandManager)
        {
            if (command is PlayCommand playCommand) playCommand.Commit();
            else if (command is TeleportCommand teleportCommand) teleportCommand.Commit();
        }
        commandManager.Clear();

        // Heals can't be undone anymore either, so their healed wounds are gone for good.
        var hand = Object.FindAnyObjectByType<PlayerHand>();
        if (hand != null) hand.PurgeHealedWounds();
    }
}
