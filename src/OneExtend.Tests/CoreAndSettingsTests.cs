using System;
using System.IO;
using System.Linq;
using OneExtend.Core.Commands;
using OneExtend.Core.Events;
using OneExtend.Core.Modules;
using OneExtend.Infrastructure.Settings;
using Xunit;

namespace OneExtend.Tests
{
    public class CoreAndSettingsTests
    {
        // ---- commands -----------------------------------------------------------

        private sealed class EchoCommand : ICommand
        {
            public static int Runs;
            public CommandMetadata Metadata { get; } = new CommandMetadata("echo", "Echo", "Test");
            public void Execute(ICommandContext context) { Runs++; }
        }

        [Fact]
        public void CommandRegistry_RegisterLookupAndDuplicate()
        {
            var registry = new CommandRegistry();
            var cmd = new EchoCommand();
            registry.Register(cmd);

            Assert.True(registry.TryGet("echo", out var found));
            Assert.Same(cmd, found);
            Assert.Throws<InvalidOperationException>(() => registry.Register(new EchoCommand()));
            Assert.False(registry.TryGet("missing", out _));
            Assert.Single(registry.All());
        }

        // ---- event bus ----------------------------------------------------------

        [Fact]
        public void EventBus_DeliversAndUnsubscribes()
        {
            var bus = new EventBus();
            var count = 0;
            var token = bus.Subscribe<string>(s => count += s.Length);

            bus.Publish("abc");
            Assert.Equal(3, count);
            Assert.Equal(1, bus.SubscriberCount<string>());

            token.Dispose();
            bus.Publish("zz");
            Assert.Equal(3, count);
            Assert.Equal(0, bus.SubscriberCount<string>());
        }

        [Fact]
        public void EventBus_SubscriberException_DoesNotBreakOthers()
        {
            var bus = new EventBus();
            var ok = false;
            bus.Subscribe<int>(_ => throw new InvalidOperationException("boom"));
            bus.Subscribe<int>(_ => ok = true);

            bus.Publish(1);

            Assert.True(ok);
        }

        // ---- module host --------------------------------------------------------

        private sealed class CountingModule : IModule
        {
            public static int Initialized;
            public string Id => "counting";
            public string Name => "Counting";
            public string Version => "1.0";
            public void Initialize(IModuleContext context) { Initialized++; }
        }

        [Fact]
        public void ModuleHost_SkipsDisabledModules()
        {
            CountingModule.Initialized = 0;
            var host = new ModuleHost();
            host.Register(new CountingModule());

            var initialized = host.InitializeEnabled(null, new System.Collections.Generic.HashSet<string> { "counting" });

            Assert.Empty(initialized);
            Assert.Equal(0, CountingModule.Initialized);
        }

        // ---- settings -----------------------------------------------------------

        [Fact]
        public void SettingsStore_RoundTrips()
        {
            var path = Path.Combine(Path.GetTempPath(), "oneextend-test-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
            try
            {
                var store = new SettingsStore(path);
                var settings = new AppSettings();
                settings.RecentLanguages = new System.Collections.Generic.List<string> { "plsql", "tsx" };
                settings.Appearance.Theme = "dark";
                store.Save(settings);

                var loaded = new SettingsStore(path).Load();

                Assert.Equal(new[] { "plsql", "tsx" }, loaded.RecentLanguages);
                Assert.Equal("dark", loaded.Appearance.Theme);
                Assert.Equal("Ctrl+Alt+C", loaded.Shortcuts["insertCodeBlock"]);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Fact]
        public void SettingsStore_MissingFile_ReturnsDefaults()
        {
            var store = new SettingsStore(Path.Combine(Path.GetTempPath(), "definitely-missing-" + Guid.NewGuid() + ".json"));

            var settings = store.Load();

            Assert.Equal("plsql", settings.General.DefaultLanguage);
            Assert.True(settings.Modules["codeblock"]);
        }

        [Fact]
        public void SettingsStore_CorruptFile_ReturnsDefaults()
        {
            var path = Path.Combine(Path.GetTempPath(), "oneextend-broken-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
            try
            {
                File.WriteAllText(path, "{ not json at all");
                var settings = new SettingsStore(path).Load();
                Assert.NotNull(settings);
                Assert.Equal("light", settings.Appearance.Theme);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
