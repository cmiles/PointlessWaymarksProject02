using System;
using System.Collections.Generic;

namespace PwTrackTrimmer.ViewModels
{
    public interface ITrackAction
    {
        string Description { get; }
        void Undo();
        void Redo();
    }

    public class UndoRedoManager
    {
        private readonly Stack<ITrackAction> _undoStack = new Stack<ITrackAction>();
        private readonly Stack<ITrackAction> _redoStack = new Stack<ITrackAction>();

        public event EventHandler StateChanged;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public void ExecuteAction(ITrackAction action)
        {
            action.Redo();
            _undoStack.Push(action);
            _redoStack.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Undo()
        {
            if (!CanUndo) return;
            var action = _undoStack.Pop();
            action.Undo();
            _redoStack.Push(action);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Redo()
        {
            if (!CanRedo) return;
            var action = _redoStack.Pop();
            action.Redo();
            _undoStack.Push(action);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
