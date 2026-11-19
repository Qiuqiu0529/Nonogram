using System.Collections.Generic;
using UnityEngine;

namespace Commands
{
    public interface ICommand
    {
        void Execute();
        void Undo();
        void ClearCommand();
    }

    public class CompositeCommand : ICommand
    {
        bool hasExcute;

        private List<ICommand> commands = new List<ICommand>();

        public int CommandSize()
        {
            return commands.Count;
        }

        public void AddCommand(ICommand command)
        {
            commands.Add(command);
        }
        public void SetHasExcute()
        {
            hasExcute = true;
        }

        public void Execute()
        {
            if (hasExcute)
            {
                return;
            }
            for (int i = 0; i < commands.Count; i++)
            {
                commands[i].Execute();
            }
        }

        public void Undo()
        {
            for (int i = commands.Count - 1; i >= 0; i--)
            {
                commands[i].Undo();
            }
        }

        public void ClearCommand()
        {
            for (int i = commands.Count - 1; i >= 0; i--)
            {
                commands[i].ClearCommand();
            }
        }
    }
}