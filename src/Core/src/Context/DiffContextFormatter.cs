﻿using System;
using System.Linq;
using System.Text;

namespace mrHelper.Core.Context
{
   /// <summary>
   /// Renders DiffContext objects into HTML web page using CSS from resources
   /// </summary>
   public class DiffContextFormatter
   {
      public DiffContextFormatter(double fontSizePx, int rowsVPaddingPx)
      {
         _fontSizePx = fontSizePx;
         _rowsVPaddingPx = rowsVPaddingPx;
      }

      public string GetStylesheet()
      {
         return loadStylesFromCSS() + getCustomStyle(_fontSizePx, _rowsVPaddingPx);
      }

      /// <summary>
      /// Throws ArgumentException if DiffContext is invalid
      /// </summary>
      public string GetBody(DiffContext context)
      {
         return getContextHTML(context);
      }

      private string getContextHTML(DiffContext ctx)
      {
         string commonBegin = @"
               <body>
                  <table cellspacing=""0"" cellpadding=""0"">
                      <tbody>";

         string commonEnd = @"
                      </tbody>
                   </table>
                </body>";

         return String.Format("{0} {1} {2}", commonBegin, getTableBody(ctx), commonEnd);
      }

      private string loadStylesFromCSS()
      {
         return mrHelper.Core.Properties.Resources.DiffContextCSS;
      }

      private string getCustomStyle(double fontSizePx, int rowsVPaddingPx)
      {
         return string.Format(@"
            table {{
               font-size: {0}px;
            }}
            td {{
               padding-top: {1}px;
               padding-bottom: {1}px;
            }}", fontSizePx, rowsVPaddingPx);
      }

      private string getTableBody(DiffContext ctx)
      {
         bool highlightSelected = ctx.Lines.Count() > 1;
         StringBuilder body = new StringBuilder();

         int iLine = 0;
         foreach (DiffContext.Line line in ctx.Lines)
         {
            body.Append("<tr");
            body.Append((iLine == ctx.SelectedIndex && highlightSelected ? " class=\"selected\"" : ""));
            body.Append(">");
            body.Append("<td class=\"linenumbers\">");
            body.Append(getLeftLineNumber(line));
            body.Append("</td>");
            body.Append("<td class=\"linenumbers\">");
            body.Append(getRightLineNumber(line));
            body.Append("</td>");
            body.Append("<td class=\"");
            body.Append(getDiffCellClass(line));
            body.Append("\">");
            body.Append(getCode(line));
            body.Append("</td>");
            body.Append("</tr>");

            ++iLine;
         }
         return body.ToString();
      }

      private string getLeftLineNumber(DiffContext.Line line)
      {
         return line.Left?.Number.ToString() ?? "";
      }

      private string getRightLineNumber(DiffContext.Line line)
      {
         return line.Right?.Number.ToString() ?? "";
      }

      private string getDiffCellClass(DiffContext.Line line)
      {
         if (line.Left.HasValue && line.Right.HasValue)
         {
            return "unchanged";
         }
         else if (line.Left.HasValue)
         {
            return line.Left.Value.State == DiffContext.Line.State.Unchanged ? "unchanged" : "removed";
         }
         else if (line.Right.HasValue)
         {
            return line.Right.Value.State == DiffContext.Line.State.Unchanged ? "unchanged" : "added";
         }

         throw new ArgumentException(String.Format("Bad context line: {0}", line.ToString()));
      }

      private string getCode(DiffContext.Line line)
      {
         if (line.Text.Length == 0)
         {
            return "<br";
         }

         string trimmed = line.Text.TrimStart();
         int leadingSpaces = line.Text.Length - trimmed.Length;

         StringBuilder result = new StringBuilder();
         for (int i = 0; i < leadingSpaces; ++i)
         {
            result.Append("&nbsp;");
         }

         result.Append(System.Net.WebUtility.HtmlEncode(trimmed));
         return result.ToString();
      }

      private readonly double _fontSizePx;
      private readonly int _rowsVPaddingPx;
   }
}

