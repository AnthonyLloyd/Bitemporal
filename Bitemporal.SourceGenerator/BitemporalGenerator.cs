using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

public class Entity { public string? Name; public List<(string Name, string Type)> Fields = new(); }

[Generator]
public class BitemporalGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context) { }
    public void Execute(GeneratorExecutionContext context)
    {
        var namespaceName = context.Compilation.AssemblyName!;
        foreach (var file in context.AdditionalFiles)
        {
            if (file.Path.EndsWith(".bitemporal"))
            {
                var archiveName = Path.GetFileNameWithoutExtension(file.Path);
                var text = file.GetText();
                if (text is null) throw new Exception(file.Path);
                var code = ArchiveText(namespaceName, archiveName, Parse(text));
                //System.Diagnostics.Debugger.Launch();
                context.AddSource(archiveName + ".cs", code);
            }
        }
    }

    List<Entity> Parse(SourceText text)
    {
        var entities = new List<Entity>();
        int line = 0;
        var lines = text.Lines;
        while (line < lines.Count)
        {
            var l = text.GetSubText(lines[line++].Span).ToString();
            if (!string.IsNullOrWhiteSpace(l))
            {
                var entity = new Entity { Name = l };
                while (line < lines.Count)
                {
                    var field = text.GetSubText(lines[line++].Span).ToString();
                    if (string.IsNullOrWhiteSpace(field)) break;
                    int i = field.IndexOf(' ');
                    var fieldName = field.Substring(0, i);
                    var fieldType = field.Substring(i + 1);
                    entity.Fields.Add((fieldName, fieldType));
                }
                entities.Add(entity);
            }
        }
        return entities;
    }

    public static string ArchiveText(string namespaceName, string archiveName, List<Entity> entities)
    {
        var sb = new StringBuilder(@"using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Bitemporal;

namespace ").A(namespaceName).A(@"
{
    public class ").A(archiveName).A(@"
    {
        uint[] Counts;
        readonly SetSlim<string> Text;");
        for (int e = 0; e < entities.Count; e++)
        {
            var fieldCount = entities[e].Fields.Count;
            for (int f = 0; f < fieldCount; f++)
                sb.A(@"
        DataSeries[] Entity").A(e).A(@"_Field").A(f).A(@";");
        }
        sb.A(@"

        public ").A(archiveName).A(@"()
        {
            Counts = new uint[").A(entities.Count).A(@"];
            Text = new();");
        for (int e = 0; e < entities.Count; e++)
        {
            var fieldCount = entities[e].Fields.Count;
            for (int f = 0; f < fieldCount; f++)
                sb.A(@"
            Entity").A(e).A(@"_Field").A(f).A(@" = Array.Empty<DataSeries>();");
        }
        sb.A(@"
        }

        public ").A(archiveName).A(@"(string filename)
        {
            using var s = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            Counts = Serialize.UIntArrayGet(s);
            Text = Serialize.SetSlimStringGet(s);");
        for (int e = 0; e < entities.Count; e++)
        {
            var fieldCount = entities[e].Fields.Count;
            for (int f = 0; f < fieldCount; f++)
                sb.A(@"
            Entity").A(e).A(@"_Field").A(f).A(@" = Serialize.DataSeriesArrayGet(s, Counts[").A(e).A(@"]);");
        }
        sb.A(@"
        }

        public void Save(string filename)
        {
            using var s = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None);
            Serialize.UIntArraySet(s, Counts);
            Serialize.SetSlimStringSet(s, Text);");
        for (int e = 0; e < entities.Count; e++)
        {
            var fieldCount = entities[e].Fields.Count;
            for (int f = 0; f < fieldCount; f++)
                sb.A(@"
            Serialize.DataSeriesArraySet(s, Entity").A(e).A(@"_Field").A(f).A(@");");
        }
        sb.A(@"
        }

        public Snapshot SnapshotLatest => new(this, Counts, new(Counts[0]-1U));
        public Snapshot SnapshotAt(TxId txId)
        {
            var newCounts = new uint[").A(entities.Count).A(@"];
            Array.Copy(Counts, 0, newCounts, 0, ").A(entities.Count).A(@");
            var i = txId.I;
            newCounts[0] = i;");
        for (int e = 1; e < entities.Count; e++)
        {
            sb.A(@"
            i = newCounts[").A(e).A(@"];
            while (Entity").A(e).A(@"_Field0[--i].After(txId)) { };
            newCounts[").A(e).A(@"] = i;");
        }
        sb.A(@"
            return new(this, newCounts, txId);
        }

        public class Snapshot
        {
            public readonly ").A(archiveName).A(@" Archive;
            public readonly uint[] Counts;
            public readonly TxId TxId;
            public TxId NextTxId => new(TxId.I + 1U);
            SetSlim<string>? Text;");
        for (int e = 0; e < entities.Count; e++)
        {
            var fieldCount = entities[e].Fields.Count;
            for (int f = 0; f < fieldCount; f++)
                sb.A(@"
            internal MapSlim<uint, DataSeries>? Entity").A(e).A(@"_Field").A(f).A(@";");
        }
        sb.A(@"
            internal Snapshot(").A(archiveName).A(@" archive, uint[] counts, TxId txId)
            {
                Archive = archive;
                Counts = counts;
                TxId = txId;
            }

            public SnapshotDate this[Date date] => new(this, date);
            internal Vint AddString(string s)
            {
                var i = Archive.Text.IndexOf(s);
                if (i >= 0) return new(i);
                return new((Text ??= new()).Add(s) + Archive.Text.Count);
            }
            internal string GetString(Vint v)
            {
                return v.I < Archive.Text.Count ? Archive.Text[(int)v.I] : Text![(int)v.I - Archive.Text.Count];
            }

            public void Commit(");
        var fields0 = entities[0].Fields;
        var fieldCount0 = fields0.Count;
        sb.A(fields0[0].Type).A(@" ").A(fields0[0].Name.ToLower());
        for (int f = 1; f < fieldCount0; f++)
            if (!fields0[f].Type.EndsWith(" Set"))
                sb.A(@", ").A(fields0[f].Type).A(@" ").A(fields0[f].Name.ToLower());
        sb.A(@")
            {
                this[time.ToDate()].TxCollection.New(");
        sb.A(fields0[0].Name.ToLower());
        for (int f = 1; f < fieldCount0; f++)
            if (!fields0[f].Type.EndsWith(" Set"))
                sb.A(@", ").A(fields0[f].Name.ToLower());
        sb.A(@");
                var newCounts = new uint[").A(entities.Count).A(@"];
                Array.Copy(Counts, 0, newCounts, 0, ").A(entities.Count).A(@");
                if(Text is not null)
                {
                    for(int i = 0; i < Text.Count; i++)
                        Archive.Text.Add(Text[i]);
                }");
        for (int e = 0; e < entities.Count; e++)
        {
            var fieldCount = entities[e].Fields.Count;
            for (int f = 0; f < fieldCount; f++)
            {
                sb.A(@"
                if (Entity").A(e).A(@"_Field").A(f).A(@" is not null)
                {
                    var maxID = 0U;
                    for(int i = Entity").A(e).A(@"_Field").A(f).A(@".Count - 1; i >= 0; i--)
                    {
                        var k = Entity").A(e).A(@"_Field").A(f).A(@".Key(i);
                        if(k > maxID) maxID = k;
                    }
                    if (Archive.Entity").A(e).A(@"_Field").A(f).A(@".Length <= maxID)
                    {
                        var a = new DataSeries[maxID + 1];
                        Array.Copy(Archive.Entity").A(e).A(@"_Field").A(f).A(@", 0, a, 0, Archive.Entity").A(e).A(@"_Field").A(f).A(@".Length);
                        Archive.Entity").A(e).A(@"_Field").A(f).A(@" = a;
                        newCounts[").A(e).A(@"] = maxID + 1;
                    }
                    foreach (var kv in Entity").A(e).A(@"_Field").A(f).A(@")
                    {
                        Archive.Entity").A(e).A(@"_Field").A(f).A(@"[kv.Key] = kv.Value;
                    }
                }");
            }
        }
        sb.A(@"
                Archive.Counts = newCounts;
            }
        }

        public class SnapshotDate
        {
            public readonly Snapshot Snapshot;
            public readonly Date Date;
            internal SnapshotDate(Snapshot snapshot, Date date)
            {
                Snapshot = snapshot;
                Date = date;
            }");
        for (int e = 0; e < entities.Count; e++)
        {
            sb.A(@"
            public ").A(entities[e].Name).A(@"Collection ").A(entities[e].Name).A(@"Collection => new(this);");
        }
        sb.A(@"
        }");
        for (int e = 0; e < entities.Count; e++)
        {
            if(e != 0)
                sb.A(@"
        public struct ").A(entities[e].Name).A(@"Id
        {
            public readonly uint I;
            public ").A(entities[e].Name).A(@"Id(uint id) => I = id;
        }
");
            sb.A(@"
        public struct ").A(entities[e].Name).A(@"Collection : IReadOnlyCollection<").A(entities[e].Name).A(@">
        {
            public readonly SnapshotDate SD;
            public ").A(entities[e].Name).A(@"Collection(SnapshotDate sd) => SD = sd;

            public IEnumerator<").A(entities[e].Name).A(@"> GetEnumerator() => Entities().GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => Entities().GetEnumerator();
            IEnumerable<").A(entities[e].Name).A(@"> Entities()
            {
                var i = SD.Snapshot.Counts[").A(e).A(@"];
                while (i > 0) yield return new(SD, new(--i));
            }
            public ").A(entities[e].Name).A(@" this[").A(entities[e].Name).A(@"Id id] => new(SD, id);");
            var fields = entities[e].Fields;
            var fieldCount = fields.Count;
                sb.A(@"
            public int Count => (int)SD.Snapshot.Counts[").A(e).A(@"];
            public ").A(entities[e].Name).A(@" New(");
            sb.A(fields[0].Type).A(@" ").A(fields[0].Name.ToLower());
            for (int f = 1; f < fieldCount; f++)
                if (!fields[f].Type.EndsWith(" Set"))
                    sb.A(@", ").A(fields[f].Type).A(@" ").A(fields[f].Name.ToLower());
            sb.A(@")
            {
                var sd = SD;
                var id = sd.Snapshot.Counts[").A(e).A(@"];
                if (sd.Snapshot.Entity").A(e).A(@"_Field0 is null)
                {");
            for (int f = 0; f < fieldCount; f++) sb.A(@"
                    sd.Snapshot.Entity").A(e).A(@"_Field").A(f).A(@" = new();");
            sb.A(@"
                }
                else
                {
                    var m = sd.Snapshot.Entity").A(e).A(@"_Field0.Key(sd.Snapshot.Entity").A(e).A(@"_Field0.Count - 1);
                    if (m >= id) id = m + 1;
                }");
            for (int f = 0; f < fieldCount; f++)
            {
                if (fields[f].Type.EndsWith(" Set"))
                    sb.A(@"
                sd.Snapshot.Entity").A(e).A(@"_Field").A(f).A(@"![id] = default;");
                else
                    sb.A(@"
                sd.Snapshot.Entity").A(e).A(@"_Field").A(f).A(@"![id] = DataSeries.Single(sd.Date, sd.Snapshot.NextTxId, ").ToVint(entities, fields[f].Type, fields[f].Name.ToLower()).A(@");");
            }
            sb.A(@"
                return new(sd, new(id));
            }");
            sb.A(@"
        }

        public struct ").A(entities[e].Name).A(@"
        {
            readonly SnapshotDate SD;
            public readonly ").A(entities[e].Name).A(@"Id Id;
            public ").A(entities[e].Name).A(@"(SnapshotDate query, ").A(entities[e].Name).A(@"Id id)
            {
                SD = query;
                Id = id;
            }");
            for (int f = 0; f < fieldCount; f++)
            {
                var field = fields[f];
                sb.A(@"
            DataSeries ").A(field.Name).A(@"_DataSeries
            {
                get
                {
                    var sd = SD;
                    int i;
                    return sd.Snapshot.Entity").A(e).A(@"_Field").A(f).A(@" is not null
                        && (i = sd.Snapshot.Entity").A(e).A(@"_Field").A(f).A(@".IndexOf(Id.I)) != -1
                         ? sd.Snapshot.Entity").A(e).A(@"_Field").A(f).A(@".Value(i)
                         : sd.Snapshot.Archive.Entity").A(e).A(@"_Field").A(f).A(@"[Id.I];
                }
                set
                {
                    (SD.Snapshot.Entity").A(e).A(@"_Field").A(f).A(@" ??= new())[Id.I] = value;
                }
            }
");
                if (field.Type.EndsWith(" Set"))
                {
                    sb.A(@"
            public struct PositionsSet : IEnumerable<Position>
            {
                [System.Diagnostics.CodeAnalysis.SuppressMessage(""Style"", ""IDE0044: Add readonly modifier"")]
                Asset E;

                public PositionsSet(Asset entity) => E = entity;

                IEnumerable<Position> Items()
                {
                    var sd = E.SD;
                    return E.Positions_DataSeries.SetGet(sd.Date, sd.Snapshot.TxId).Select(i => new Position(sd, new((uint)i.I)));
                }

                public IEnumerator<Position> GetEnumerator() => Items().GetEnumerator();

                IEnumerator IEnumerable.GetEnumerator() => Items().GetEnumerator();

                public void Add(Position p)
                {
                    var sd = E.SD;
                    E.Positions_DataSeries = E.Positions_DataSeries.SetAdd(sd.Date, sd.Snapshot.NextTxId, new(p.Id.I));
                }

                public void Remove(Position p)
                {
                    var sd = E.SD;
                    E.Positions_DataSeries = E.Positions_DataSeries.SetRemove(sd.Date, sd.Snapshot.NextTxId, new(p.Id.I));
                }
            }

            public PositionsSet Positions => new(this);");
                }
                else
                {
                    sb.A(@"
            public ").A(field.Type).A(@" ").A(field.Name).A(@"
            {
                get
                {
                    var sd = SD;
                    var ds = ").A(field.Name).A(@"_DataSeries;
                    return ").ToType(entities, field.Type, "ds[sd.Date, sd.Snapshot.TxId].Item3").A(@";
                }
                set
                {
                    var sd = SD;
                    ").A(field.Name).A(@"_DataSeries = ").A(field.Name).A(@"_DataSeries.Add(sd.Date, sd.Snapshot.NextTxId, ").ToVint(entities, field.Type, "value").A(@");
                }
            }

            public IEnumerable<(Date, TxId, ").A(field.Type).A(@")> ").A(field.Name).A(@"_Audit
            {
                get
                {
                    var sd = SD;
                    var date = sd.Date;
                    var txId = sd.Snapshot.TxId;
                    return ").A(field.Name).A(@"_DataSeries
                           .Where(i => i.Item1 <= date && i.Item2 <= txId)
                           .Select(i => (i.Item1, i.Item2, ").ToType(entities, field.Type, "i.Item3").A(@"));
                }
            }

            public (Date, TxId, ").A(field.Type).A(@") ").A(field.Name).A(@"_Detail
            {
                get
                {
                    var sd = SD;
                    var row = ").A(field.Name).A(@"_DataSeries[sd.Date, sd.Snapshot.TxId];
                    return (row.Item1, row.Item2, ").ToType(entities, field.Type, "row.Item3").A(@");
                }
            }");
                }
            }
            sb.A(@"
        }");
        }
        sb.A(@"
    }
}");
        return sb.ToString();
    }
}

public static class StringBuilderExtensions
{
    public static StringBuilder A(this StringBuilder sb, string? s) => sb.Append(s);
    public static StringBuilder A(this StringBuilder sb, int i) => sb.Append(i);
    public static StringBuilder ToVint(this StringBuilder sb, List<Entity> entities, string type, string name)
    {
        return entities.Any(i => i.Name == type) ? sb.Append("new(").Append(name).Append(@".Id.I)")
             : type == "string" ? sb.Append("sd.Snapshot.AddString(").Append(name).Append(@")")
             : sb.Append(name).Append(@".ToVint()");
    }
    public static StringBuilder ToType(this StringBuilder sb, List<Entity> entities, string type, string name)
    {
        return entities.Any(i => i.Name == type) ? sb.Append("new ").Append(type).Append(@"(sd, new((uint)").Append(name).Append(@".I))")
             : type == "string" ? sb.Append("sd.Snapshot.GetString(").Append(name).Append(@")")
             : sb.Append(name).Append(@".To").Append(type).Append(@"()");
    }
}