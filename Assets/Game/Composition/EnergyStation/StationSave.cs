using System;
using System.IO;
using System.Linq;
using AFrame.Foundation;
using AFrame.Game.Exit;
using AFrame.Runtime;
using AFrame.Save;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AFrame.Game.EnergyStation
{
    internal sealed class StationSave
    {
        private readonly AtomicJsonFile file;
        private readonly Float3 bounds;
        private readonly int total;
        private readonly float timeLimit;

        public StationSave(string path, Float3 bounds, int total, float timeLimit)
        {
            file = new AtomicJsonFile(path);
            this.bounds = bounds;
            this.total = total;
            this.timeLimit = timeLimit;
        }

        public bool Exists => file.Exists;

        public void Save(ModuleContext context)
        {
            var position = context.Get<ISnapshotParticipant<Float3>>().Capture();
            var cores = context.Get<ISnapshotParticipant<int[]>>().Capture();
            var progress = context.Get<ISnapshotParticipant<StationProgress>>().Capture();
            var save = new JObject
            {
                ["version"] = 1,
                ["x"] = position.x,
                ["z"] = position.z,
                ["cores"] = new JArray(cores),
                ["seconds"] = progress.SecondsRemaining,
                ["cleared"] = progress.Cleared,
                ["timedOut"] = progress.TimedOut
            };
            file.Write(save.ToString(Formatting.Indented));
        }

        public Action<ModuleContext> Read()
        {
            var data = JObject.Parse(file.Read(), new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            if (data["version"]?.Type != JTokenType.Integer || data["version"].ToString() != "1")
                throw new InvalidDataException("不支持此存档版本。");

            var position = new Float3((float)Number(data["x"]), 0, (float)Number(data["z"]));
            if (Math.Abs(position.x) > bounds.x || Math.Abs(position.z) > bounds.z)
                throw new InvalidDataException("存档中的角色位置无效。");
            if (!(data["cores"] is JArray items) || items.Count > total)
                throw new InvalidDataException("存档中的能源核心无效。");
            var cores = items.Select(item => item.Type == JTokenType.Integer && Number(item) >= 0 && Number(item) < total
                ? (int)item : throw new InvalidDataException("存档中的核心编号无效。")).ToArray();
            var seconds = Number(data["seconds"]);
            var cleared = Boolean(data["cleared"]);
            var timedOut = Boolean(data["timedOut"]);
            if (cores.Distinct().Count() != cores.Length || seconds < 0 || seconds > timeLimit
                || timedOut != (seconds == 0) || cleared && (timedOut || cores.Length != total))
                throw new InvalidDataException("存档中的关卡状态无效。");
            var progress = new StationProgress(seconds, cleared, timedOut);

            return context =>
            {
                var character = context.Get<ISnapshotParticipant<Float3>>();
                var inventory = context.Get<ISnapshotParticipant<int[]>>();
                var exit = context.Get<ISnapshotParticipant<StationProgress>>();
                character.Validate(position);
                inventory.Validate(cores);
                exit.Validate(progress);
                character.Restore(position);
                inventory.Restore(cores);
                exit.Restore(progress);
            };
        }

        private static double Number(JToken token)
        {
            if (token?.Type != JTokenType.Float && token?.Type != JTokenType.Integer)
                throw new InvalidDataException("存档缺少有效的数值。");
            var value = (double)token;
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidDataException("存档包含无效的数值。");
            return value;
        }

        private static bool Boolean(JToken token) => token?.Type == JTokenType.Boolean
            ? (bool)token : throw new InvalidDataException("存档缺少关卡状态。");
    }
}
