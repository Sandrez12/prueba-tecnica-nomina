using System.Text;

namespace Nomina.Web.Payroll;

public static class PayrollCsv
{
    public static byte[] Write(PayrollReport report)
    {
        var csv = new StringBuilder();
        csv.Append("Periodo;Documento;Persona;Base COP;Laborados;Ausencias;Descuento ausencia COP;Incapacidades;Descuento incapacidad COP;Adicionales;Pago adicional COP;Ajuste mínimo COP;Total COP\r\n");
        var period = FormattableString.Invariant($"{report.Year:D4}-{report.Month:D2}");
        foreach (var line in report.Lines)
        {
            csv.Append(period).Append(';').Append(Text(line.Document)).Append(';')
                .Append(Text(line.PersonName)).Append(';')
                .Append(FormattableString.Invariant($"{line.MonthlySalary};{line.WorkedDays};{line.AbsentDays};{line.AbsenceAdjustment};{line.SickDays};{line.SicknessAdjustment};{line.ExtraDays};{line.ExtraAdjustment};{line.MinimumAdjustment};{line.Total}"))
                .Append("\r\n");
        }
        csv.Append(FormattableString.Invariant($"{period};;TOTAL DEL PERÍODO;;;;;;;;;;{report.Total}\r\n"));
        var data = Encoding.UTF8.GetBytes(csv.ToString());
        return Encoding.UTF8.GetPreamble().Concat(data).ToArray();
    }

    private static string Text(string value)
    {
        var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        var start = normalized.AsSpan().TrimStart();
        if (!start.IsEmpty && start[0] is '=' or '+' or '-' or '@')
            normalized = "'" + normalized;
        return "\"" + normalized.Replace("\"", "\"\"") + "\"";
    }
}
