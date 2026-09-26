using System;
using System.Linq;
using OneExtend.CodeBlock.Formatting;
using Xunit;

namespace OneExtend.Tests
{
    public class SqlFormatterTests
    {
        private static string Format(string sql, KeywordCase keywordCase = KeywordCase.Upper, CommaPosition comma = CommaPosition.Trailing)
        {
            var formatter = new SqlFormatter(TestHost.TokenizerFor("plsql"));
            return formatter.Format(sql, new FormatOptions
            {
                KeywordCase = keywordCase,
                CommaPosition = comma,
                IndentWidth = 4
            });
        }

        private static string[] Lines(string sql, KeywordCase kc = KeywordCase.Upper) =>
            Format(sql, kc).Split('\n');

        [Fact]
        public void BasicSelect_ClausesAlignedAndAndIndented()
        {
            var lines = Lines("select a,b from t where x=1 and y=2 order by a;");

            Assert.Equal("SELECT a,", lines[0]);
            Assert.Equal("       b", lines[1]);
            Assert.Equal("    FROM t", lines[2]);
            Assert.Equal("   WHERE x = 1", lines[3]);
            Assert.Equal("   AND y = 2", lines[4]);
            Assert.Equal("ORDER BY a;", lines[5]);
        }

        [Fact]
        public void KeywordCase_CanBePreservedOrLowered()
        {
            Assert.StartsWith("SELECT", Format("select 1 from t", KeywordCase.Upper));
            Assert.StartsWith("select", Format("select 1 from t", KeywordCase.Lower));
            Assert.StartsWith("Select", Format("select 1 from t", KeywordCase.Capitalize));
            Assert.StartsWith("sElEcT", Format("sElEcT 1 from t", KeywordCase.Preserve));
        }

        [Fact]
        public void Joins_EachOnOwnLine()
        {
            var output = Format(
                "select o.id from orders o join customers c on c.id=o.cid left join items i on i.oid=o.id");

            Assert.Contains("JOIN customers c ON c.id = o.cid", output);
            Assert.Contains("LEFT JOIN items i ON i.oid = o.id", output);
            Assert.DoesNotContain("JOIN customers c ON c.id = o.cid LEFT", output);
        }

        [Fact]
        public void CaseExpression_IsLaidOutMultiLine()
        {
            var lines = Lines("select case when x=1 then 'a' when x=2 then 'b' else 'c' end from t");

            Assert.Equal("SELECT CASE", lines[0]);
            Assert.Equal("  WHEN x = 1 THEN 'a'", lines[1]);
            Assert.Equal("  WHEN x = 2 THEN 'b'", lines[2]);
            Assert.Equal("  ELSE 'c'", lines[3]);
            Assert.Equal("END", lines[4]);
        }

        [Fact]
        public void Subquery_IsIndented()
        {
            var output = Format("select * from t where id in (select id from t2)");

            Assert.Contains("WHERE id IN (", output);
            Assert.Contains("\n    SELECT id", output);
            Assert.Contains("\n    FROM t2", output);
            Assert.Contains("\n)", output);
        }

        [Fact]
        public void MultipleStatements_EachLaidOut()
        {
            var output = Format("select 1 from a; select 2 from b;");

            Assert.Contains("SELECT 1", output);
            Assert.Contains("SELECT 2", output);
            Assert.Equal(2, output.Split('\n').Count(l => l.TrimStart().StartsWith("SELECT")));
        }

        [Fact]
        public void UpdateStatement_SetItemsAligned()
        {
            var output = Format("update t set a=1, b=2 where id=3;", KeywordCase.Upper);

            Assert.Contains("UPDATE t", output);
            Assert.Contains("SET a = 1,", output);
            Assert.Contains("\n      b = 2", output);
            Assert.Contains("WHERE id = 3;", output);
        }

        [Fact]
        public void BindVariablesAndStrings_SurviveLayout()
        {
            var output = Format("select * from t where name='O''Brien' and amount>:lo and note like q'[50%]'", KeywordCase.Upper);

            Assert.Contains("'O''Brien'", output);
            Assert.Contains(":lo", output);
            Assert.Contains("q'[50%]'", output);
        }

        [Fact]
        public void InList_StaysInline()
        {
            var output = Format("select * from t where id in (1, 2, 3)");
            Assert.Contains("IN (1, 2, 3)", output);
        }

        [Fact]
        public void UnaryMinusSticksToOpenParen()
        {
            var output = Format("select raise_application_error(-20001, 'x') from t");
            Assert.Contains("(-20001,", output);
        }

        [Fact]
        public void AnsiGrammar_ProducesSameClauseLayout()
        {
            var formatter = new SqlFormatter(TestHost.TokenizerFor("sql-ansi"));
            var output = formatter.Format(
                "select a from t where x=1 and y=2",
                new FormatOptions { KeywordCase = KeywordCase.Upper });

            Assert.Contains("SELECT a", output);
            Assert.Contains("WHERE x = 1", output);
            Assert.Contains("AND y = 2", output);
        }
    }
}
