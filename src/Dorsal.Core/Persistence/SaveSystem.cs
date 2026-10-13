using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dorsal.Core.Career;
using Dorsal.Core.World;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Dorsal.Core.Persistence
{
    /// <summary>Contenido completo de una partida guardada.</summary>
    public sealed class SaveGame
    {
        public GameWorld World { get; set; } = new GameWorld();
        public CareerState Career { get; set; } = new CareerState();

        public static SaveGame From(CareerSession session)
        {
            session.SyncRngToState();
            return new SaveGame { World = session.World, Career = session.State };
        }

        public CareerSession ToSession() => CareerSession.Restore(World, Career);
    }

    /// <summary>Resumen visible en la lista de partidas (no requiere descomprimir el contenido).</summary>
    public sealed class SaveSummary
    {
        public string PlayerName { get; set; } = "";
        public string ClubName { get; set; } = "";
        public int SeasonYear { get; set; }
        public int Age { get; set; }
        public int Overall { get; set; }
        public string Phase { get; set; } = "";
    }

    /// <summary>Envoltorio del archivo: cabecera legible + contenido comprimido con suma de comprobación.</summary>
    public sealed class SaveEnvelope
    {
        public const string FormatId = "dorsal-save";

        public string Format { get; set; } = FormatId;
        public int SchemaVersion { get; set; }
        public string GameVersion { get; set; } = "";
        public DateTime SavedUtc { get; set; }
        public string Slot { get; set; } = "";
        public SaveSummary Summary { get; set; } = new SaveSummary();

        /// <summary>SHA-256 (hex) de los bytes comprimidos del contenido.</summary>
        public string PayloadSha256 { get; set; } = "";

        /// <summary>JSON del <see cref="SaveGame"/> comprimido con GZip y codificado en Base64.</summary>
        public string Payload { get; set; } = "";
    }

    /// <summary>Migración del contenido JSON de una versión de esquema a la siguiente.</summary>
    public interface ISaveMigration
    {
        int FromVersion { get; }
        void Migrate(JObject saveGame);
    }

    /// <summary>Almacenamiento abstracto (Unity: Application.persistentDataPath; pruebas: memoria).</summary>
    public interface ISaveStorage
    {
        bool Exists(string name);
        byte[] Read(string name);
        void Write(string name, byte[] data);
        void Delete(string name);
        void Move(string from, string to);
        IEnumerable<string> List();
    }

    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string _dir;

        public FileSaveStorage(string directory)
        {
            _dir = directory;
            Directory.CreateDirectory(_dir);
        }

        public bool Exists(string name) => File.Exists(Path.Combine(_dir, name));
        public byte[] Read(string name) => File.ReadAllBytes(Path.Combine(_dir, name));

        public void Write(string name, byte[] data)
        {
            var path = Path.Combine(_dir, name);
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(data, 0, data.Length);
                fs.Flush(true); // forzar a disco antes de renombrar
            }
        }

        public void Delete(string name)
        {
            var path = Path.Combine(_dir, name);
            if (File.Exists(path)) File.Delete(path);
        }

        public void Move(string from, string to)
        {
            var dst = Path.Combine(_dir, to);
            if (File.Exists(dst)) File.Delete(dst);
            File.Move(Path.Combine(_dir, from), dst);
        }

        public IEnumerable<string> List() => Directory.GetFiles(_dir).Select(Path.GetFileName).Where(n => n != null).Select(n => n!);
    }

    public sealed class MemorySaveStorage : ISaveStorage
    {
        public Dictionary<string, byte[]> Files { get; } = new Dictionary<string, byte[]>();
        public bool Exists(string name) => Files.ContainsKey(name);
        public byte[] Read(string name) => Files.TryGetValue(name, out var b) ? b.ToArray() : throw new FileNotFoundException(name);
        public void Write(string name, byte[] data) => Files[name] = data.ToArray();
        public void Delete(string name) => Files.Remove(name);

        public void Move(string from, string to)
        {
            Files[to] = Files[from];
            Files.Remove(from);
        }

        public IEnumerable<string> List() => Files.Keys.ToList();
    }

    public sealed class LoadResult
    {
        public bool Success { get; set; }
        public SaveGame? Game { get; set; }
        public bool UsedBackup { get; set; }
        public int MigratedFrom { get; set; }
        public string Error { get; set; } = "";
    }

    /// <summary>
    /// Guardado fiable: escritura atómica (temporal → verificación → copia de seguridad → renombrado),
    /// suma SHA-256, recuperación desde la copia anterior y migraciones de esquema.
    /// </summary>
    public sealed class SaveManager
    {
        public const int CurrentSchemaVersion = 1;
        public const string SaveExtension = ".sav";
        public const string BackupExtension = ".bak";
        public const string TempExtension = ".tmp";

        private readonly ISaveStorage _storage;
        private readonly Dictionary<int, ISaveMigration> _migrations = new Dictionary<int, ISaveMigration>();
        private readonly int _schemaVersion;

        public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Converters = { new StringEnumConverter() },
            NullValueHandling = NullValueHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            DateTimeZoneHandling = DateTimeZoneHandling.Unspecified,
            Formatting = Formatting.None,
        };

        public SaveManager(ISaveStorage storage, string gameVersion = "0.2.0", int schemaVersion = CurrentSchemaVersion)
        {
            _storage = storage;
            GameVersion = gameVersion;
            _schemaVersion = schemaVersion;
        }

        public string GameVersion { get; }

        public void RegisterMigration(ISaveMigration m) => _migrations[m.FromVersion] = m;

        public static string SlotFile(string slot) => slot + SaveExtension;

        public void Save(string slot, SaveGame game)
        {
            ValidateSlot(slot);
            var bytes = Serialize(slot, game);
            string tmp = slot + TempExtension, sav = SlotFile(slot), bak = slot + BackupExtension;
            _storage.Write(tmp, bytes);
            // Verificar lo escrito antes de sustituir nada.
            var check = TryDecode(_storage.Read(tmp), out _, out var error);
            if (check == null)
            {
                _storage.Delete(tmp);
                throw new IOException("La verificación del guardado falló: " + error);
            }
            if (_storage.Exists(sav)) _storage.Move(sav, bak);
            _storage.Move(tmp, sav);
        }

        public LoadResult Load(string slot)
        {
            ValidateSlot(slot);
            var result = new LoadResult();
            string sav = SlotFile(slot), bak = slot + BackupExtension;
            string error = "No existe la partida.";
            if (_storage.Exists(sav))
            {
                var g = TryDecode(_storage.Read(sav), out int migrated, out error);
                if (g != null) return new LoadResult { Success = true, Game = g, MigratedFrom = migrated };
            }
            if (_storage.Exists(bak))
            {
                var g = TryDecode(_storage.Read(bak), out int migrated, out var bakError);
                if (g != null) return new LoadResult { Success = true, Game = g, UsedBackup = true, MigratedFrom = migrated, Error = error };
                error += " Copia de seguridad: " + bakError;
            }
            result.Error = error;
            return result;
        }

        public IEnumerable<(string slot, SaveEnvelope header)> ListSlots()
        {
            foreach (var name in _storage.List().Where(n => n.EndsWith(SaveExtension, StringComparison.Ordinal)).OrderBy(n => n))
            {
                SaveEnvelope? env = null;
                try { env = JsonConvert.DeserializeObject<SaveEnvelope>(Encoding.UTF8.GetString(_storage.Read(name)), JsonSettings); }
                catch (JsonException) { }
                if (env != null) yield return (name.Substring(0, name.Length - SaveExtension.Length), env);
            }
        }

        public void Delete(string slot)
        {
            ValidateSlot(slot);
            _storage.Delete(SlotFile(slot));
            _storage.Delete(slot + BackupExtension);
            _storage.Delete(slot + TempExtension);
        }

        /// <summary>Copia de seguridad exportable (el mismo formato del archivo).</summary>
        public byte[] Export(string slot) => _storage.Read(SlotFile(slot));

        /// <summary>Importa una copia exportada, validándola antes de escribirla.</summary>
        public void Import(string slot, byte[] data)
        {
            var g = TryDecode(data, out _, out var error) ?? throw new InvalidDataException("Archivo no válido: " + error);
            Save(slot, g);
        }

        public byte[] Serialize(string slot, SaveGame game)
        {
            string json = JsonConvert.SerializeObject(game, JsonSettings);
            byte[] compressed = Gzip(Encoding.UTF8.GetBytes(json));
            var user = game.World.Players.TryGetValue(game.Career.UserPlayerId, out var u) ? u : null;
            var env = new SaveEnvelope
            {
                SchemaVersion = _schemaVersion,
                GameVersion = GameVersion,
                SavedUtc = DateTime.UtcNow,
                Slot = slot,
                Summary = new SaveSummary
                {
                    PlayerName = user?.FullName ?? "",
                    ClubName = user?.ClubId != null && game.World.Clubs.TryGetValue(user.ClubId.Value, out var c) ? c.Name : "Sin club",
                    SeasonYear = game.World.SeasonYear,
                    Age = user?.AgeOn(game.World.CurrentDate) ?? 0,
                    Overall = user?.Overall ?? 0,
                    Phase = game.Career.Phase.ToString(),
                },
                PayloadSha256 = Sha256Hex(compressed),
                Payload = Convert.ToBase64String(compressed),
            };
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(env, Formatting.Indented, JsonSettings));
        }

        private SaveGame? TryDecode(byte[] data, out int migratedFrom, out string error)
        {
            migratedFrom = 0;
            error = "";
            try
            {
                var env = JsonConvert.DeserializeObject<SaveEnvelope>(Encoding.UTF8.GetString(data), JsonSettings);
                if (env == null || env.Format != SaveEnvelope.FormatId) { error = "Formato desconocido."; return null; }
                if (env.SchemaVersion > _schemaVersion) { error = $"La partida es de una versión más nueva ({env.SchemaVersion})."; return null; }
                byte[] compressed = Convert.FromBase64String(env.Payload);
                if (!string.Equals(Sha256Hex(compressed), env.PayloadSha256, StringComparison.OrdinalIgnoreCase))
                {
                    error = "Suma de comprobación incorrecta: el archivo está dañado.";
                    return null;
                }
                string json = Encoding.UTF8.GetString(Gunzip(compressed));
                var obj = JObject.Parse(json);
                int version = env.SchemaVersion;
                if (version < _schemaVersion) migratedFrom = version;
                while (version < _schemaVersion)
                {
                    if (!_migrations.TryGetValue(version, out var m)) { error = $"Falta la migración desde la versión {version}."; return null; }
                    m.Migrate(obj);
                    version++;
                }
                var game = obj.ToObject<SaveGame>(JsonSerializer.Create(JsonSettings));
                if (game == null) { error = "Contenido vacío."; return null; }
                var problems = game.World.Validate();
                if (problems.Count > 0) { error = "Datos incoherentes: " + problems[0]; return null; }
                if (!game.World.Players.ContainsKey(game.Career.UserPlayerId)) { error = "Falta el jugador del usuario."; return null; }
                return game;
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is InvalidDataException || ex is IOException)
            {
                error = ex.Message;
                return null;
            }
        }

        private static void ValidateSlot(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot) || slot.Length > 40 || slot.Any(c => !(char.IsLetterOrDigit(c) || c == '-' || c == '_')))
                throw new ArgumentException("Nombre de ranura no válido (letras, números, '-' y '_').");
        }

        private static byte[] Gzip(byte[] data)
        {
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true)) gz.Write(data, 0, data.Length);
            return ms.ToArray();
        }

        private static byte[] Gunzip(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var gz = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gz.CopyTo(output);
            return output.ToArray();
        }

        private static string Sha256Hex(byte[] data)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
    }
}
