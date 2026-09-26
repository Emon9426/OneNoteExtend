using OneExtend.CodeBlock.Formatting;
using Xunit;

namespace OneExtend.Tests
{
    public class PlSqlFormatterTests
    {
        private static string[] Format(string source, KeywordCase kc = KeywordCase.Upper)
        {
            var formatter = new PlSqlFormatter(TestHost.TokenizerFor("plsql"));
            return formatter.Format(source, new FormatOptions { KeywordCase = kc, IndentWidth = 4 })
                             .Split('\n');
        }

        private static string FirstContaining(string[] lines, string fragment)
        {
            foreach (var l in lines)
                if (l.ToUpperInvariant().Contains(fragment.ToUpperInvariant()))
                    return l;
            throw new Xunit.Sdk.XunitException(
                $"No line contains '{fragment}'. Output:\n{string.Join("\n", lines)}");
        }

        [Fact]
        public void AnonymousBlock_Structure()
        {
            var lines = Format(
                "declare v_total number := 0; " +
                "begin " +
                "for rec in (select amount from orders where status='PAID') loop " +
                "v_total := v_total + rec.amount; " +
                "end loop; " +
                "dbms_output.put_line('Total: ' || v_total); " +
                "end;");

            Assert.Equal("DECLARE", lines[0]);
            Assert.Equal("   v_total NUMBER := 0;", lines[1]);   // +3 under DECLARE
            Assert.Equal("BEGIN", lines[2]);

            var forLine = FirstContaining(lines, "FOR rec IN (");
            Assert.StartsWith("   ", forLine);          // +3 under BEGIN

            var selectLine = FirstContaining(lines, "SELECT amount");
            Assert.StartsWith("      ", selectLine);    // +3 under FOR

            Assert.Equal("   ) LOOP", FirstContaining(lines, ") LOOP"));
            Assert.Equal("      v_total := v_total + rec.amount;",
                FirstContaining(lines, "v_total := v_total + rec.amount;"));
            Assert.Equal("   END LOOP;", FirstContaining(lines, "END LOOP;"));
            Assert.StartsWith("   ", FirstContaining(lines, "DBMS_OUTPUT.PUT_LINE"));
            Assert.Equal("END;", lines[lines.Length - 1]);
        }

        [Fact]
        public void ExceptionSection_WhenBranchesAligned()
        {
            var lines = Format(
                "begin " +
                "raise_application_error(-20001,'x'); " +
                "exception " +
                "when no_data_found then null; " +
                "when others then " +
                "dbms_output.put_line(sqlerrm); " +
                "end;");

            Assert.Equal("BEGIN", lines[0]);
            var exceptionLine = FirstContaining(lines, "EXCEPTION");
            Assert.StartsWith("EXCEPTION", exceptionLine);       // at column 0

            var when1 = FirstContaining(lines, "WHEN NO_DATA_FOUND THEN");
            Assert.StartsWith("  ", when1);                        // +2 under EXCEPTION

            var when2 = FirstContaining(lines, "WHEN OTHERS THEN");
            Assert.StartsWith("  ", when2);                        // sibling alignment

            var body = FirstContaining(lines, "DBMS_OUTPUT.PUT_LINE(SQLERRM)");
            Assert.StartsWith("     ", body);                      // +3 under WHEN

            Assert.Equal("END;", lines[lines.Length - 1]);
        }

        [Fact]
        public void IfElsifElse_Nesting()
        {
            var lines = Format(
                "begin " +
                "if a=1 then b:=2; elsif a=2 then b:=3; else b:=4; end if; " +
                "end;");

            Assert.StartsWith("   ", FirstContaining(lines, "IF a = 1 THEN"));
            Assert.StartsWith("      ", FirstContaining(lines, "b := 2;"));
            Assert.StartsWith("   ELSIF", FirstContaining(lines, "ELSIF a = 2 THEN"));
            Assert.StartsWith("      ", FirstContaining(lines, "b := 3;"));
            Assert.StartsWith("   ELSE", FirstContaining(lines, "ELSE"));
            Assert.StartsWith("      ", FirstContaining(lines, "b := 4;"));
            Assert.Equal("   END IF;", FirstContaining(lines, "END IF;"));
            Assert.Equal("END;", lines[lines.Length - 1]);
        }

        [Fact]
        public void EmbeddedSelect_GetsSqlLayout()
        {
            var lines = Format(
                "begin " +
                "delete from audit_log where created_at < sysdate - 30; " +
                "commit; " +
                "end;");

            Assert.StartsWith("   DELETE FROM audit_log", FirstContaining(lines, "DELETE FROM audit_log"));
            Assert.StartsWith("   WHERE created_at <", FirstContaining(lines, "WHERE created_at"));
            Assert.Equal("   COMMIT;", FirstContaining(lines, "COMMIT;"));
        }

        [Fact]
        public void KeywordCase_UpperByDefault_PreserveOptional()
        {
            var upper = string.Join("\n", Format("begin null; end;"));
            Assert.Contains("BEGIN", upper);

            var preserved = string.Join("\n", Format("begin null; end;", KeywordCase.Preserve));
            Assert.Contains("begin", preserved);
            Assert.Contains("null", preserved);
        }
    }
}
