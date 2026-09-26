using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OneExtend.Core.Commands
{
    public interface ICommandRegistry
    {
        void Register(ICommand command);
        bool TryGet(string id, out ICommand command);
        IReadOnlyList<ICommand> All();
        IReadOnlyList<ICommand> InCategory(string category);
    }

    /// <summary>Central, thread-safe command catalog shared by every module.</summary>
    public sealed class CommandRegistry : ICommandRegistry
    {
        private readonly ConcurrentDictionary<string, ICommand> _commands =
            new ConcurrentDictionary<string, ICommand>(StringComparer.Ordinal);

        public void Register(ICommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (!_commands.TryAdd(command.Metadata.Id, command))
                throw new InvalidOperationException(
                    $"A command with id '{command.Metadata.Id}' is already registered.");
        }

        public bool TryGet(string id, out ICommand command)
        {
            if (id == null)
            {
                command = null;
                return false;
            }
            return _commands.TryGetValue(id, out command);
        }

        public IReadOnlyList<ICommand> All() =>
            _commands.Values.OrderBy(c => c.Metadata.Category)
                             .ThenBy(c => c.Metadata.Title)
                             .ToList();

        public IReadOnlyList<ICommand> InCategory(string category) =>
            _commands.Values.Where(c => string.Equals(c.Metadata.Category, category, StringComparison.OrdinalIgnoreCase))
                             .OrderBy(c => c.Metadata.Title)
                             .ToList();
    }
}
