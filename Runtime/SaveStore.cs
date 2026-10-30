using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ITKamianets.Engine.Save
{
	/// <summary>
	/// Local save slots for one game, one JSON file per slot under
	/// <c>Application.persistentDataPath/&lt;gameId&gt;/&lt;slot&gt;.json</c>.
	/// The file is an envelope (3d-save-schema) around a payload the game defines and versions.
	/// </summary>
	public sealed class SaveStore
	{
		/// <summary>Version of the envelope, the <c>schemaVersion</c> field of 3d-save-schema.</summary>
		public const int SchemaVersion = 1;

		readonly string _directory;
		readonly bool _keepBackup;

		public string GameId { get; }

		/// <param name="gameId">Stable id of the game, for example <c>hippodrome</c>.</param>
		/// <param name="rootDirectory">Where the game's folder is made; the persistent data path when null.</param>
		/// <param name="keepBackup">Keep the previous good save of a slot beside it (<c>&lt;slot&gt;.backup.json</c>) each time it is replaced.</param>
		public SaveStore(string gameId, string rootDirectory = null, bool keepBackup = false)
		{
			_keepBackup = keepBackup;
			if (string.IsNullOrWhiteSpace(gameId)) throw new ArgumentException("gameId is required", nameof(gameId));
			GameId = gameId;
			_directory = Path.Combine(rootDirectory ?? Application.persistentDataPath, gameId);
		}

		public string PathFor(string slot) => Path.Combine(_directory, slot + ".json");

		public bool Exists(string slot) => File.Exists(PathFor(slot));

		public string BackupPathFor(string slot) => Path.Combine(_directory, slot + ".backup.json");

		public bool BackupExists(string slot) => File.Exists(BackupPathFor(slot));

		/// <summary>
		/// Writes the slot: to a temporary file first, then swapped in, so a crash never leaves half a save.
		/// </summary>
		/// <param name="payloadVersion">Version of the game's own payload; raise it when its shape changes.</param>
		public SaveStatus Save<T>(string slot, T payload, int payloadVersion = 1)
		{
			try
			{
				Directory.CreateDirectory(_directory);
				var envelope = new JObject
				{
					["schemaVersion"] = SchemaVersion,
					["gameId"] = GameId,
					["slotId"] = slot,
					["savedAt"] = DateTime.UtcNow.ToString("o"),
					["payloadVersion"] = payloadVersion,
					["payload"] = payload == null ? new JObject() : JToken.FromObject(payload),
				};
				var path = PathFor(slot);
				var temp = path + ".tmp";
				File.WriteAllText(temp, envelope.ToString(Formatting.Indented));
				if (_keepBackup && File.Exists(path) && IsReadable(path)) File.Copy(path, BackupPathFor(slot), true);
				if (File.Exists(path)) File.Replace(temp, path, null);
				else File.Move(temp, path);
				return SaveStatus.Ok;
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[Save] could not write slot '{slot}': {e.Message}");
				return SaveStatus.IoError;
			}
		}

		/// <summary>
		/// Reads the slot. A save with an older payload version goes through <paramref name="migrate"/>
		/// (old payload, its version → current payload); without it such a save is reported as unsupported.
		/// An unreadable file is renamed aside, never deleted.
		/// </summary>
		public SaveResult<T> Load<T>(string slot, int currentPayloadVersion = 1, Func<JToken, int, T> migrate = null)
			=> Read(PathFor(slot), currentPayloadVersion, migrate, true);

		/// <summary>
		/// Reads the backup of a slot (the save before the last one, needs <c>keepBackup</c>). A damaged backup is reported as
		/// <see cref="SaveStatus.Corrupt"/> and left where it is.
		/// </summary>
		public SaveResult<T> LoadBackup<T>(string slot, int currentPayloadVersion = 1, Func<JToken, int, T> migrate = null)
			=> Read(BackupPathFor(slot), currentPayloadVersion, migrate, false);

		static bool IsReadable(string path)
		{
			try { return JObject.Parse(File.ReadAllText(path))["payload"] != null; }
			catch (Exception) { return false; }
		}

		static SaveResult<T> Read<T>(string path, int currentPayloadVersion, Func<JToken, int, T> migrate, bool setAsideWhenCorrupt)
		{
			if (!File.Exists(path)) return new SaveResult<T>(SaveStatus.NotFound, default);

			string text;
			try { text = File.ReadAllText(path); }
			catch (Exception e) { return new SaveResult<T>(SaveStatus.IoError, default, e.Message); }

			try
			{
				var envelope = JObject.Parse(text);
				var schema = (int?)envelope["schemaVersion"] ?? 0;
				var version = (int?)envelope["payloadVersion"] ?? 1;
				if (schema > SchemaVersion || version > currentPayloadVersion)
					return new SaveResult<T>(SaveStatus.UnsupportedVersion, default, $"saved by a newer version (schema {schema}, payload {version})");
				var payload = envelope["payload"];
				if (payload == null) throw new JsonException("no payload");

				if (version < currentPayloadVersion)
				{
					if (migrate == null) return new SaveResult<T>(SaveStatus.UnsupportedVersion, default, $"payload version {version} needs a migration");
					return new SaveResult<T>(SaveStatus.Ok, migrate(payload, version));
				}
				return new SaveResult<T>(SaveStatus.Ok, payload.ToObject<T>());
			}
			catch (Exception e) when (e is JsonException || e is FormatException || e is InvalidCastException)
			{
				if (setAsideWhenCorrupt) KeepCorrupt(path);
				return new SaveResult<T>(SaveStatus.Corrupt, default, e.Message);
			}
		}

		public void Delete(string slot)
		{
			var path = PathFor(slot);
			if (File.Exists(path)) File.Delete(path);
			var backup = BackupPathFor(slot);
			if (File.Exists(backup)) File.Delete(backup);
		}

		static void KeepCorrupt(string path)
		{
			try
			{
				var aside = path.Substring(0, path.Length - ".json".Length) + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".json";
				File.Move(path, aside);
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[Save] could not set aside a corrupt save: {e.Message}");
			}
		}
	}
}
