using System;

namespace OneExtend.Core.Commands
{
    /// <summary>
    /// Ambient services handed to a command at execution time.
    /// Intentionally narrow in v0.1; will grow (active page, selection...) with future modules.
    /// </summary>
    public interface ICommandContext
    {
        IServiceProvider Services { get; }
    }

    /// <summary>A single user-invocable action.</summary>
    public interface ICommand
    {
        CommandMetadata Metadata { get; }

        void Execute(ICommandContext context);
    }
}
