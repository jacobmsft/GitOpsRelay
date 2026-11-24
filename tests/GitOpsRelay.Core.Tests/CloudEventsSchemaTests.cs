using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GitOpsRelay.Contracts;
using GitOpsRelay.Core.Normalization;

namespace GitOpsRelay.Core.Tests;

public sealed class CloudEventsSchemaTests
{
    [Fact]
    public void EverySignalKindConformsToCanonicalSchema()
    {
        var normalizer = new GitSignalNormalizer();
        _signals.Clear();
        AddRefSignals("refs/heads/feature", "branch", null, TestData.Sha1);
        AddRefSignals("refs/heads/feature", "branch", TestData.Sha1, null);
        AddRefSignals("refs/tags/v1", "tag", null, TestData.Sha1);
        AddRefSignals("refs/tags/v1", "tag", TestData.Sha1, null);
        AddRefSignals("refs/heads/main", "branch", TestData.Sha1, TestData.Sha1Next);
        AddRefSignals("refs/heads/new", "branch", null, TestData.Sha1, onlyPush: true);

        foreach (var (eventType, status) in new[]
        {
            (GitSignalTypes.PullRequestOpened, "open"),
            (GitSignalTypes.PullRequestUpdated, "open"),
            (GitSignalTypes.PullRequestMerged, "merged"),
            (GitSignalTypes.PullRequestClosed, "closed")
        })
        {
            _signals.Add(normalizer.CreatePullRequestSignal(
                TestData.Repository(),
                TestData.Ingestion(),
                new PullRequestInfo
                {
                    Id = $"pr-{_signals.Count}",
                    Number = _signals.Count + 1,
                    Title = "Schema test",
                    Status = status,
                    IsDraft = false,
                    SourceRef = "feature",
                    TargetRef = "main",
                    HeadSha = TestData.Sha1,
                    MergeCommitSha = status == "merged" ? TestData.Sha1Next : null
                },
                eventType: eventType));
        }

        foreach (var signal in _signals)
        {
            AssertSchemaValid(signal);
        }

        var branchCreated = _signals.Single(signal => signal.Type == GitSignalTypes.BranchCreated);
        var branchDeleted = _signals.Single(signal => signal.Type == GitSignalTypes.BranchDeleted);
        Assert.Null(branchCreated.Data.Change!.BeforeSha);
        Assert.Null(branchDeleted.Data.Change!.AfterSha);
    }

    [Fact]
    public void SchemaAllowsTraceparentAndRejectsCredentialBearingCloneUrls()
    {
        var normalizer = new GitSignalNormalizer();
        var signal = normalizer.CreateRefTransitionSignals(
            TestData.Repository(),
            TestData.Ingestion(),
            "refs/heads/main",
            "branch",
            null,
            TestData.Sha1)[1];
        signal.Extensions = new Dictionary<string, JsonElement>
        {
            ["traceparent"] = JsonDocument.Parse("\"00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01\"").RootElement.Clone()
        };
        AssertSchemaValid(signal);

        var pattern = Schema.GetProperty("$defs").GetProperty("data")
            .GetProperty("properties").GetProperty("repository")
            .GetProperty("properties").GetProperty("cloneUrl").GetProperty("pattern").GetString()!;
        var cloneUrlPattern = new Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Matches(cloneUrlPattern, "https://github.com/Octo/App.git");
        Assert.Matches(cloneUrlPattern, "git@github.com:Octo/App.git");
        Assert.Matches(cloneUrlPattern, "deploy@host.example:org/repo.git");
        Assert.Matches(cloneUrlPattern, "ssh://git@github.com/Octo/App.git");
        Assert.DoesNotMatch(cloneUrlPattern, "https://user:secret@github.com/Octo/App.git");
        Assert.DoesNotMatch(cloneUrlPattern, "https://github.com/Octo/App.git?access_token=secret");
        Assert.DoesNotMatch(cloneUrlPattern, "https://github.com/Octo/App.git#fragment");
        Assert.DoesNotMatch(cloneUrlPattern, "file:///tmp/repo");
    }

    [Fact]
    public void SchemaRejectsInvalidOidRefTypeAndCreatedTransition()
    {
        var normalizer = new GitSignalNormalizer();
        var created = normalizer.CreateRefTransitionSignals(
            TestData.Repository(), TestData.Ingestion(), "refs/heads/main", "branch", null, TestData.Sha1)[0];
        var createdJson = JsonNode.Parse(JsonSerializer.Serialize(
            created, GitSignalJsonContext.Default.GitSignal))!;
        createdJson["data"]!["change"]!["beforeSha"] = TestData.Sha1;
        AssertSchemaInvalid(createdJson);

        var pushed = normalizer.CreateRefTransitionSignals(
            TestData.Repository(), TestData.Ingestion(), "refs/heads/main", "branch",
            TestData.Sha1, TestData.Sha1Next)[0];
        var zeroOidJson = JsonNode.Parse(JsonSerializer.Serialize(
            pushed, GitSignalJsonContext.Default.GitSignal))!;
        zeroOidJson["data"]!["change"]!["afterSha"] = new string('0', 40);
        AssertSchemaInvalid(zeroOidJson);

        var refTypeJson = JsonNode.Parse(JsonSerializer.Serialize(
            pushed, GitSignalJsonContext.Default.GitSignal))!;
        refTypeJson["data"]!["change"]!["refType"] = "tag";
        AssertSchemaInvalid(refTypeJson);
    }

    [Fact]
    public void ExtensionAttributesRejectReservedNamesInvalidNamesAndStructuredValues()
    {
        var signal = new GitSignalNormalizer().CreateRefTransitionSignals(
            TestData.Repository(), TestData.Ingestion(), "refs/heads/main", "branch", null, TestData.Sha1)[1];
        Assert.Throws<ArgumentException>(() => signal.Extensions = new Dictionary<string, JsonElement>
        {
            ["data"] = JsonDocument.Parse("1").RootElement
        });
        Assert.Throws<ArgumentException>(() => signal.Extensions = new Dictionary<string, JsonElement>
        {
            ["TraceParent"] = JsonDocument.Parse("\"bad\"").RootElement
        });
        Assert.Throws<ArgumentException>(() => signal.Extensions = new Dictionary<string, JsonElement>
        {
            ["custom"] = JsonDocument.Parse("{}").RootElement
        });
        Assert.Equal("https://example.test/", GitSignal.CreateExtensionValue(new Uri("https://example.test/")).GetString());
    }

    private static JsonElement Schema => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "schema", "gitsignal.cloudevents.schema.json"))).RootElement;

    private static void AssertSchemaValid(GitSignal signal)
    {
        using var messageDocument = JsonDocument.Parse(JsonSerializer.Serialize(
            signal, GitSignalJsonContext.Default.GitSignal));
        var errors = SchemaValidator.Validate(Schema, messageDocument.RootElement, Schema);
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    private static void AssertSchemaInvalid(JsonNode message)
    {
        var messageElement = JsonSerializer.SerializeToElement(message);
        Assert.NotEmpty(SchemaValidator.Validate(Schema, messageElement, Schema));
    }

    private void AddRefSignals(
        string refName,
        string refType,
        string? beforeSha,
        string? afterSha,
        bool onlyPush = false)
    {
        var signals = new GitSignalNormalizer().CreateRefTransitionSignals(
            TestData.Repository(), TestData.Ingestion(), refName, refType, beforeSha, afterSha);
        _signals.AddRange(onlyPush ? signals.Where(signal => signal.Type == GitSignalTypes.Push) : signals);
    }

    private readonly List<GitSignal> _signals = [];

    private static class SchemaValidator
    {
        public static IReadOnlyList<string> Validate(JsonElement schema, JsonElement value, JsonElement root)
        {
            var errors = new List<string>();
            Apply(schema, value, root, "$", errors);
            return errors;
        }

        private static void Apply(
            JsonElement schema,
            JsonElement value,
            JsonElement root,
            string path,
            List<string> errors)
        {
            if (schema.TryGetProperty("$ref", out var reference))
            {
                var target = root;
                foreach (var segment in reference.GetString()!.Split('/').Skip(1))
                {
                    target = target.GetProperty(segment);
                }

                Apply(target, value, root, path, errors);
            }

            if (schema.TryGetProperty("type", out var type) && !MatchesType(type, value))
            {
                errors.Add($"{path}: expected {type} but found {value.ValueKind}.");
                return;
            }
            if (schema.TryGetProperty("const", out var constant) && !JsonEquals(constant, value))
            {
                errors.Add($"{path}: value does not match const.");
            }
            if (schema.TryGetProperty("enum", out var enumValues)
                && !enumValues.EnumerateArray().Any(option => JsonEquals(option, value)))
            {
                errors.Add($"{path}: value is not in enum.");
            }
            if (schema.TryGetProperty("pattern", out var pattern)
                && value.ValueKind == JsonValueKind.String
                && !Regex.IsMatch(value.GetString()!, pattern.GetString()!, RegexOptions.CultureInvariant))
            {
                errors.Add($"{path}: string does not match pattern.");
            }
            if (schema.TryGetProperty("format", out var format)
                && format.GetString() == "date-time"
                && value.ValueKind == JsonValueKind.String
                && !DateTimeOffset.TryParse(value.GetString(), out _))
            {
                errors.Add($"{path}: invalid date-time.");
            }
            if (schema.TryGetProperty("minLength", out var minLength)
                && value.ValueKind == JsonValueKind.String
                && value.GetString()!.Length < minLength.GetInt32())
            {
                errors.Add($"{path}: string is shorter than minLength.");
            }
            if (schema.TryGetProperty("minimum", out var minimum)
                && value.ValueKind == JsonValueKind.Number
                && value.GetDouble() < minimum.GetDouble())
            {
                errors.Add($"{path}: number is below minimum.");
            }
            if (schema.TryGetProperty("required", out var required) && value.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in required.EnumerateArray())
                {
                    if (!value.TryGetProperty(property.GetString()!, out _))
                    {
                        errors.Add($"{path}: missing required property {property.GetString()}.");
                    }
                }
            }
            if (schema.TryGetProperty("properties", out var properties) && value.ValueKind == JsonValueKind.Object)
            {
                foreach (var propertySchema in properties.EnumerateObject())
                {
                    if (value.TryGetProperty(propertySchema.Name, out var propertyValue))
                    {
                        Apply(propertySchema.Value, propertyValue, root, $"{path}.{propertySchema.Name}", errors);
                    }
                }
            }
            if (schema.TryGetProperty("patternProperties", out var patternProperties)
                && value.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in value.EnumerateObject())
                {
                    foreach (var patternSchema in patternProperties.EnumerateObject())
                    {
                        if (Regex.IsMatch(property.Name, patternSchema.Name, RegexOptions.CultureInvariant))
                        {
                            Apply(patternSchema.Value, property.Value, root, $"{path}.{property.Name}", errors);
                        }
                    }
                }
            }
            if (schema.TryGetProperty("additionalProperties", out var additional)
                && additional.ValueKind == JsonValueKind.False
                && value.ValueKind == JsonValueKind.Object)
            {
                var known = schema.TryGetProperty("properties", out var knownProperties)
                    ? knownProperties.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject())
                {
                    var matchedByPattern = schema.TryGetProperty("patternProperties", out var patterns)
                        && patterns.EnumerateObject().Any(item => Regex.IsMatch(property.Name, item.Name, RegexOptions.CultureInvariant));
                    if (!known.Contains(property.Name) && !matchedByPattern)
                    {
                        errors.Add($"{path}: additional property {property.Name} is not allowed.");
                    }
                }
            }
            if (schema.TryGetProperty("items", out var items) && value.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    Apply(items, item, root, $"{path}[{index++}]", errors);
                }
            }
            if (schema.TryGetProperty("not", out var notSchema)
                && Validate(notSchema, value, root).Count == 0)
            {
                errors.Add($"{path}: value matches a forbidden schema.");
            }
            if (schema.TryGetProperty("allOf", out var allOf))
            {
                foreach (var subschema in allOf.EnumerateArray())
                {
                    Apply(subschema, value, root, path, errors);
                }
            }
            if (schema.TryGetProperty("if", out var condition))
            {
                var matches = Validate(condition, value, root).Count == 0;
                var branch = matches ? "then" : "else";
                if (schema.TryGetProperty(branch, out var selected))
                {
                    Apply(selected, value, root, path, errors);
                }
            }
        }

        private static bool MatchesType(JsonElement type, JsonElement value)
        {
            var accepted = type.ValueKind == JsonValueKind.Array
                ? type.EnumerateArray().Select(item => item.GetString()!)
                : [type.GetString()!];
            return accepted.Any(item => item switch
            {
                "object" => value.ValueKind == JsonValueKind.Object,
                "array" => value.ValueKind == JsonValueKind.Array,
                "string" => value.ValueKind == JsonValueKind.String,
                "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "null" => value.ValueKind == JsonValueKind.Null,
                "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
                "number" => value.ValueKind == JsonValueKind.Number,
                _ => false
            });
        }

        private static bool JsonEquals(JsonElement left, JsonElement right) =>
            left.ValueKind == right.ValueKind
                && (left.ValueKind == JsonValueKind.String
                    ? left.GetString() == right.GetString()
                    : left.ValueKind == JsonValueKind.Null
                        || left.GetRawText() == right.GetRawText());
    }
}
