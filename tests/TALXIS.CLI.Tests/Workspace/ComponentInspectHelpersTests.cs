using System.Xml.Linq;
using TALXIS.CLI.Features.Workspace;
using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.Serialization.Xml;
using Xunit;

namespace TALXIS.CLI.Tests.Workspace;

/// <summary>
/// Unit tests for <see cref="ComponentInspectHelpers"/> - form tree projection
/// (tab/section/control with labels), depth pruning, and entity attribute projection.
/// </summary>
public class ComponentInspectHelpersTests
{
    private const string FormBodyXml = """
        <form>
          <tabs>
            <tab verticallayout="true" id="{f9470000-9798-48da-a0f6-95e0369e77f5}" name="general_tab">
              <labels>
                <label description="Obecné" languagecode="1029" />
                <label description="General" languagecode="1033" />
              </labels>
              <columns>
                <column width="100%">
                  <sections>
                    <section showlabel="false" id="{f3339070-6ea9-4af0-97ad-481dc1be290e}" name="general_section">
                      <labels>
                        <label description="Details" languagecode="1033" />
                      </labels>
                      <rows>
                        <row>
                          <cell id="{bd5c0e53-2aca-4cfb-9678-2ce5252e8cc7}">
                            <labels>
                              <label description="Name" languagecode="1033" />
                            </labels>
                            <control id="ppf_name" classid="{4273EDBD-AC1D-40d3-9FB2-095C621B552D}" datafieldname="ppf_name" />
                          </cell>
                        </row>
                        <row>
                          <cell id="{11111111-2aca-4cfb-9678-2ce5252e8cc7}">
                            <labels>
                              <label description="Číslo účtu" languagecode="1029" />
                            </labels>
                            <control id="ppf_accountnumber" classid="{4273EDBD-AC1D-40d3-9FB2-095C621B552D}" datafieldname="ppf_accountnumber" />
                          </cell>
                        </row>
                      </rows>
                    </section>
                  </sections>
                </column>
              </columns>
            </tab>
          </tabs>
        </form>
        """;

    private static FormMetadata CreateForm() => new()
    {
        FormId = "{af7d924d-aeef-4023-b307-5939da109f64}",
        FormType = "main",
        EntityLogicalName = "ppf_bankaccount",
        DisplayName = new Label("Information", 1033),
        Body = MergeableNodeXmlConverter.FromXElement(XElement.Parse(FormBodyXml)),
    };

    [Fact]
    public void BuildFormResult_FullDepth_ProjectsTabsSectionsControls()
    {
        var result = ComponentInspectHelpers.BuildFormResult(CreateForm(), depth: null);

        Assert.Equal("{af7d924d-aeef-4023-b307-5939da109f64}", result.FormId);
        Assert.Equal("main", result.FormType);
        Assert.Equal("ppf_bankaccount", result.EntityLogicalName);
        Assert.Equal("Information", result.DisplayName);

        var tab = Assert.Single(result.Tabs);
        Assert.Equal("general_tab", tab.Name);
        Assert.Equal("General", tab.Label);

        var section = Assert.Single(tab.Sections!);
        Assert.Equal("Details", section.Label);

        Assert.Equal(2, section.Controls!.Count);
        var control = section.Controls[0];
        Assert.Equal("ppf_name", control.DataFieldName);
        Assert.Equal("{4273EDBD-AC1D-40d3-9FB2-095C621B552D}", control.ClassId);
        Assert.Equal("Name", control.Label);
    }

    [Fact]
    public void BuildFormResult_ControlLabel_FallsBackToNonEnglishCellLabel()
    {
        var result = ComponentInspectHelpers.BuildFormResult(CreateForm(), depth: null);

        var controls = result.Tabs[0].Sections![0].Controls!;
        Assert.Equal("Číslo účtu", controls[1].Label);
    }

    [Fact]
    public void BuildFormResult_Depth1_OmitsSections()
    {
        var result = ComponentInspectHelpers.BuildFormResult(CreateForm(), depth: 1);

        var tab = Assert.Single(result.Tabs);
        Assert.Equal("General", tab.Label);
        Assert.Null(tab.Sections);
    }

    [Fact]
    public void BuildFormResult_Depth2_OmitsControls()
    {
        var result = ComponentInspectHelpers.BuildFormResult(CreateForm(), depth: 2);

        var section = Assert.Single(result.Tabs[0].Sections!);
        Assert.Equal("Details", section.Label);
        Assert.Null(section.Controls);
    }

    [Fact]
    public void BuildEntityResult_ProjectsAttributesWithRequiredLevels()
    {
        var entity = new EntityMetadata
        {
            LogicalName = "ppf_bankaccount",
            SchemaName = "ppf_BankAccount",
            DisplayName = new Label("Bank Account", 1033),
            PrimaryIdAttribute = "ppf_bankaccountid",
            PrimaryNameAttribute = "ppf_name",
            Ownership = OwnershipType.UserOwned,
            IsCustomEntity = true,
        };
        entity.AddAttribute(new StringAttributeMetadata
        {
            LogicalName = "ppf_name",
            DisplayName = new Label("Name", 1033),
            RequiredLevel = RequiredLevel.ApplicationRequired,
            IsCustomAttribute = true,
        });
        entity.AddAttribute(new LookupAttributeMetadata
        {
            LogicalName = "ppf_ownerid",
            RequiredLevel = RequiredLevel.None,
        });

        var result = ComponentInspectHelpers.BuildEntityResult(entity);

        Assert.Equal("ppf_bankaccount", result.LogicalName);
        Assert.Equal("Bank Account", result.DisplayName);
        Assert.Equal("ppf_name", result.PrimaryNameAttribute);
        Assert.Equal("UserOwned", result.Ownership);

        Assert.Equal(2, result.Attributes.Count);
        var name = result.Attributes[0];
        Assert.Equal("String", name.Type);
        Assert.Equal("ApplicationRequired", name.RequiredLevel);
        Assert.True(name.IsRequired);

        var owner = result.Attributes[1];
        Assert.Equal("Lookup", owner.Type);
        Assert.False(owner.IsRequired);
    }

    [Theory]
    [InlineData("{AF7D924D-AEEF-4023-B307-5939DA109F64}", "af7d924d-aeef-4023-b307-5939da109f64")]
    [InlineData("af7d924d-aeef-4023-b307-5939da109f64", "af7d924d-aeef-4023-b307-5939da109f64")]
    [InlineData("not-a-guid", null)]
    [InlineData(null, null)]
    public void NormalizeGuid_HandlesBracesCaseAndGarbage(string? input, string? expected)
    {
        Assert.Equal(expected, ComponentInspectHelpers.NormalizeGuid(input));
    }
}
