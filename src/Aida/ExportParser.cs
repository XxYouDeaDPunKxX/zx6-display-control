using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
namespace ZX6DisplayControl {
 public static class ExportParser {
  public static SensorReadResult Parse(byte[] buffer,long nowMilliseconds) {
   if(buffer==null || buffer.Length==0 || buffer.Length>1048576) return Invalid("Invalid AIDA64 export size.");
   try {
    bool bom=buffer.Length>=2 && buffer[0]==255 && buffer[1]==254;
    bool wide=bom || (buffer.Length>=2 && buffer[1]==0 && buffer[0]!=0);
    int start=bom?2:0,end=-1;
    if(wide) {for(int i=start;i+1<buffer.Length;i+=2) if(buffer[i]==0 && buffer[i+1]==0){end=i;break;}}
    else {for(int i=0;i<buffer.Length;i++) if(buffer[i]==0){end=i;break;}}
    if(end<=start) return Invalid("AIDA64 export is empty or incomplete.");
    string text=(wide?new UnicodeEncoding(false,false,true):Encoding.Default).GetString(buffer,start,end-start);
    var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1048600};
    XDocument document;
    using(var input=new StringReader("<export>"+text+"</export>")) using(var reader=XmlReader.Create(input,settings)) document=XDocument.Load(reader);
    var values=new Dictionary<string,SensorValue>(StringComparer.Ordinal);
    foreach(var node in document.Root.Elements()) {
     string id=Field(node,"id"),label=Field(node,"label"),raw=Field(node,"value");
     if(string.IsNullOrWhiteSpace(id) || raw==null || values.ContainsKey(id)) return Invalid("AIDA64 export contains a missing or duplicate ID, or an incomplete value.");
     double parsed; double? number=null;
     if(double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out parsed) && !double.IsNaN(parsed) && !double.IsInfinity(parsed)) number=parsed;
     string kind=node.Name.LocalName;
     values.Add(id,new SensorValue(id,label??id,kind,UnitFor(kind,id),raw,number));
    }
    if(values.Count==0) return Invalid("No sensors exported by AIDA64.");
    return SensorReadResult.Success(new SensorSnapshot(values,nowMilliseconds));
   } catch(XmlException) {return Invalid("AIDA64 export is incomplete or invalid.");}
     catch(DecoderFallbackException) {return Invalid("Invalid AIDA64 export encoding.");}
     catch(FormatException) {return Invalid("Ambiguous AIDA64 export fields.");}
  }
  private static string Field(XElement node,string name) {
   var fields=node.Elements(name).ToArray();
   if(fields.Length>1 || (fields.Length==1 && fields[0].HasElements)) throw new FormatException();
   return fields.Length==0?null:fields[0].Value;
  }
  private static SensorReadResult Invalid(string message) {return SensorReadResult.Failure("InvalidExport",message);}
  private static string UnitFor(string kind,string id) {
   switch(kind) {case "temp":return "°C";case "fan":return "RPM";case "duty":return "%";case "volt":return "V";case "curr":return "A";case "pwr":return "W";}
   if(id=="SCPUUTI" || id=="SMEMUTI" || System.Text.RegularExpressions.Regex.IsMatch(id,@"^S(CPU\d+|GPU\d+)UTI$")) return "%";
   return null;
  }
 }
}
