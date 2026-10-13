using Dorsal.Core.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Dorsal.Core.Simulation
{
    /// <summary>Carga reglas de competición desde JSON (contenido de datos, no código).</summary>
    public static class RulesLoader
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Converters = { new StringEnumConverter() },
            MissingMemberHandling = MissingMemberHandling.Error,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        public static CompetitionRules FromJson(string json)
        {
            var rules = JsonConvert.DeserializeObject<CompetitionRules>(json, Settings)
                        ?? throw new JsonException("JSON de reglas vacío.");
            rules.Validate();
            return rules;
        }

        public static string ToJson(CompetitionRules rules) => JsonConvert.SerializeObject(rules, Formatting.Indented, Settings);
    }
}
