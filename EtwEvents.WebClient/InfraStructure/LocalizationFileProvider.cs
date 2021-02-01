using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Localization;

namespace KdSoft.EtwEvents
{
    class LocalizationFileProvider: ILocalizationFileLocationProvider
    {
        readonly IFileProvider _fileProvider;
        readonly string _resourcesContainer;

        public LocalizationFileProvider(IHostEnvironment hostingEnvironment, IOptions<LocalizationOptions> localizationOptions) {
            _fileProvider = hostingEnvironment.ContentRootFileProvider;
            _resourcesContainer = localizationOptions.Value.ResourcesPath;
        }

        public IEnumerable<IFileInfo> GetLocations(string cultureName) {
            // Load .po files in each addin folder first
            var dirContents = _fileProvider.GetDirectoryContents("EventSinks");
            foreach (var fi in dirContents) {
                if (fi.IsDirectory) {
                    yield return _fileProvider.GetFileInfo(Path.Combine(fi.Name, _resourcesContainer, $"{cultureName}.po"));
                }
            }
            yield return _fileProvider.GetFileInfo(Path.Combine(_resourcesContainer, cultureName + ".po"));
        }
    }
}
