using System;
using System.Collections.Generic;
using System.Linq;
namespace ZX6DisplayControl {
 public static class SensorCatalog {
  public static IReadOnlyList<SensorValue> Filter(SensorSnapshot snapshot,string query,string kind,bool temperaturesOnly) {
   if(snapshot==null) return new List<SensorValue>().AsReadOnly();
   query=query??"";
   return snapshot.Values.Values.Where(s=>(!temperaturesOnly || (s.Kind=="temp" && s.Number.HasValue)) && (string.IsNullOrEmpty(kind) || s.Kind==kind) && (s.Id.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0 || s.Label.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)).OrderBy(s=>s.Label,StringComparer.CurrentCultureIgnoreCase).ThenBy(s=>s.Id,StringComparer.Ordinal).ToList().AsReadOnly();
  }
 }
}
