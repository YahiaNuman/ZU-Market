using System.Collections;
using System.Globalization;
using System.Resources;
using Microsoft.Extensions.Localization;

namespace ZuMarket.Services;

public class EmbeddedResourceStringLocalizerFactory : IStringLocalizerFactory
{
    public IStringLocalizer Create(Type resourceSource) => new EmbeddedResourceStringLocalizer();
    public IStringLocalizer Create(string baseName, string location) => new EmbeddedResourceStringLocalizer();
}

public class EmbeddedResourceStringLocalizer : IStringLocalizer
{
    private static readonly ResourceManager English = new("ZuMarket.Resources.SharedResource", typeof(SharedResource).Assembly);
    private static readonly ResourceManager Arabic = new("ZuMarket.Resources.SharedResource.ar", typeof(SharedResource).Assembly);

    public LocalizedString this[string name]
    {
        get
        {
            var value = CurrentManager.GetString(name, CultureInfo.InvariantCulture);
            return new LocalizedString(name, value ?? name, value is null);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var localized = this[name];
            return new LocalizedString(name, string.Format(CultureInfo.CurrentCulture, localized.Value, arguments), localized.ResourceNotFound);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var set = CurrentManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
        if (set is null) yield break;
        foreach (DictionaryEntry item in set)
        {
            var key = item.Key?.ToString();
            if (!string.IsNullOrEmpty(key)) yield return new LocalizedString(key, item.Value?.ToString() ?? key);
        }
    }

    private static ResourceManager CurrentManager => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? Arabic : English;
}
