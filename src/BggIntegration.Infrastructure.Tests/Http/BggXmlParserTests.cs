using BggIntegration.Infrastructure.Http;
using Shouldly;
using System.Xml.Linq;
using Xunit;

namespace BggIntegration.Infrastructure.Tests.Http;

public sealed class BggXmlParserTests
{
	[Fact]
	public void ParseSearchResults_ReturnsPrimaryNameAndYear()
	{
		var doc = XDocument.Parse("""
			<items>
			  <item id="13"><name type="primary" value="Catan"/><yearpublished value="1995"/></item>
			  <item id="14"><name type="alternate" value="Siedler"/></item>
			</items>
			""");

		var results = BggXmlParser.ParseSearchResults(doc);

		var result = results.ShouldHaveSingleItem();
		result.BggId.ShouldBe(13);
		result.Name.ShouldBe("Catan");
		result.Year.ShouldBe(1995);
	}

	[Fact]
	public void ParseGameDetails_EmptyDocument_ReturnsNull()
	{
		BggXmlParser.ParseGameDetails(XDocument.Parse("<items/>"), 13).ShouldBeNull();
	}

	[Fact]
	public void ParseGameDetails_ReadsNameAndPlayerCounts()
	{
		var doc = XDocument.Parse("""
			<items><item id="13">
			  <name type="primary" value="Catan"/>
			  <minplayers value="3"/><maxplayers value="4"/>
			</item></items>
			""");

		var details = BggXmlParser.ParseGameDetails(doc, 13);

		details.ShouldNotBeNull();
		details.Name.ShouldBe("Catan");
		details.MinPlayers.ShouldBe(3);
		details.MaxPlayers.ShouldBe(4);
	}

	[Fact]
	public void ParseExpansionLinks_IgnoresInboundLinks()
	{
		var doc = XDocument.Parse("""
			<items><item id="13">
			  <link type="boardgameexpansion" id="100" value="Seafarers"/>
			  <link type="boardgameexpansion" id="200" value="Base Game" inbound="true"/>
			</item></items>
			""");

		var links = BggXmlParser.ParseExpansionLinks(doc);

		links.ShouldHaveSingleItem().Name.ShouldBe("Seafarers");
	}
}