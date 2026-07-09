using DotNetCampus.Localizations;

namespace DotNetCampus.SlingNetwork.Localizations;

[LocalizedConfiguration(Default = "en",
    GenerationMode = GenerationMode.Dictionary,
    DependencyMode = DependencyMode.NestedSource,
    NotificationMode = NotificationMode.InitOnly,
    EnsureKeysIdentical = true)]
public partial class LocalizedText;
