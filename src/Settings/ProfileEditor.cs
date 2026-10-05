using System;
using System.Collections.Generic;
namespace ZX6DisplayControl {
 public sealed class ProfileEditor {
  private Profile active;
  public ProfileEditor(Profile active) {if(active==null) throw new ArgumentNullException("active");this.active=active.Copy();Draft=active.Copy();}
  public Profile Draft {get;private set;}
  public IReadOnlyList<string> Validate(SensorSnapshot catalog) {return Draft.Validate();}
  public Profile Apply() {if(Validate(null).Count>0) throw new InvalidOperationException("Fix the profile settings before applying.");active=Draft.Copy();return active.Copy();}
  public void Cancel() {Draft=active.Copy();}
 }
}
