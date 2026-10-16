using System.Runtime.CompilerServices;
using System.Windows.Markup;

[assembly: XmlnsDefinition("urn:pcl-dsh:core:ui:animation", "PCL.Core.UI.Animation")]
[assembly: XmlnsDefinition("urn:pcl-dsh:core:ui:animation", "PCL.Core.UI.Animation.Core")]
[assembly: XmlnsDefinition("urn:pcl-dsh:core:ui:animation", "PCL.Core.UI.Animation.Easings")]
[assembly: XmlnsPrefix("urn:pcl-dsh:core:ui:animation", "ani")]

[assembly:XmlnsDefinition("urn:pcl-dsh:core:utils:validate", "PCL.Core.Utils.Validate")]
[assembly:XmlnsPrefix("urn:pcl-dsh:core:utils:validate", "val")]

[assembly: DisableRuntimeMarshalling]
[assembly: InternalsVisibleTo("PCL.Core.Test")]
