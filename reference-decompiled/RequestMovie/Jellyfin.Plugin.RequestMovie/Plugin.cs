using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.RequestMovie.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.RequestMovie;

public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
	public static readonly Guid PluginId = new Guid("f8517621-7c60-4e19-9696-67f9b31fa092");

	public static Plugin? Instance { get; private set; }

	public override string Name => "Request Movie";

	public override Guid Id => PluginId;

	public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
		: base(applicationPaths, xmlSerializer)
	{
		Instance = this;
	}

	public IEnumerable<PluginPageInfo> GetPages()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected Obj, but got Unknown
		return new _003C_003Ez__ReadOnlySingleElementList<PluginPageInfo>(new PluginPageInfo
		{
			Name = ((BasePlugin)this).Name,
			EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", ((object)this).GetType().Namespace)
		});
	}
}
