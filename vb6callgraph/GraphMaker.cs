﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace vb6callgraph
{
    public class GraphMaker
    {
        public string CommentOut = @"(^[\s]*('.*)$)|(^[^""']*(""[^""]*(("""")[^""]*)*""[^""']*)*('.*)$)";
        public string StringLiteral = @"""(""""|[^""]+)""*";
        public string SubFuncDef = @"^\s*(?'ispublic'Private|Public)\s+(Sub|Function)\s+(?'name'\w[\w\d]+)\(";
        public string SubFuncCall = @"(?'name'\w[\w\d]+)";
        public string StmtBlock = @"(^[\s]*)(End (If|While|Loop|Next( [A-z0-9_]+)?|Sub|Function|With))([\s]*$)";
        public void MakerMain(string[] files)
        {
            var commentOut = new Regex(CommentOut);
            var stringLiteral = new Regex(StringLiteral);
            var subFuncDef = new Regex(SubFuncDef);
            var subFuncCall = new Regex(SubFuncCall);
            var stmtBlock = new Regex(StmtBlock);
            var anz = new Dictionary<string, Analyzer.VBMethod>();
            var children = new Dictionary<string, List<string>>();
            var matrix = new Analyzer.GraphMatrix();
            foreach (string file in files)
            {
                var lines = File.ReadAllLines(file, Encoding.Default);
                var lineno = 0;
                var mdlnm = Path.GetFileName(file);
                var methodName = string.Empty;
                foreach (string line in lines)
                {
                    lines[lineno] = commentOut.Replace(lines[lineno], string.Empty);
                    if (!lines[lineno].Trim().EndsWith(" _"))    // 継続行なし
                    {
                        lines[lineno] = stringLiteral.Replace(lines[lineno], string.Empty);
                        var matches = subFuncDef.Matches(lines[lineno]);
                        if (matches.Count > 0)
                        {
                            methodName = matches[0].Groups["name"].Value;
                            anz.Add(methodName, new Analyzer.VBMethod()
                            {
                                Name = methodName,
                                ModuleName = mdlnm,
                                IsPublic = matches[0].Groups["ispublic"].Value == "Public",
                                StartLine = lineno + 1,
                                Parents = new List<Analyzer.VBMethod>(),
                            });
                            children.Add(methodName, new List<string>());
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(methodName))
                            {
                                var stmtMatches = stmtBlock.Matches(lines[lineno]);
                                if (stmtMatches.Count > 0)
                                {
                                    var end = stmtMatches[0].Groups[0].Value;
                                    if (!string.IsNullOrEmpty(methodName) && anz.ContainsKey(methodName) && anz[methodName].ModuleName == mdlnm)
                                    {
                                        if (end == "End Sub" || end == "End Function")
                                        {
                                            anz[methodName].EndLine = lineno + 1;
                                        }
                                    }
                                    lines[lineno] = stmtBlock.Replace(lines[lineno], string.Empty);
                                }
                                var matchesCall = subFuncCall.Matches(lines[lineno]);
                                var nowlist = children[methodName];
                                for (int i = 0; i < matchesCall.Count; i++)
                                {
                                    nowlist.Add(matchesCall[i].Value);
                                }
                                nowlist = nowlist.Distinct().ToList();
                                children[methodName] = nowlist;
                            }
                        }
                    }
                    else
                    {
                        // 次の行と結合して保管
                        if (lines.Length - 1 > lineno)
                        {
                            lines[lineno + 1] = lines[lineno].Trim().Replace(" _", "") + lines[lineno + 1];
                            lines[lineno] = string.Empty;
                        }
                    }
                    lineno++;
                }
            }
            var keys = children.Keys.ToList();
            foreach (string method in keys)
            {
                children[method] = children[method].Intersect(keys).Except(new string[] { method }).ToList();
                anz[method].Children = children[method].Select(c => anz[c]).ToList();
                children[method].ForEach(c => anz[c].Parents.Add(anz[method]));
            }
            matrix.Cells = new List<Analyzer.VBMethod>(anz.Count);
            var heads = anz.Values.ToList();
            heads.Sort(cmp);
        }

        private int cmp(Analyzer.VBMethod x, Analyzer.VBMethod y)
        {
            return x.Parents.Count - y.Parents.Count;
        }
    }
}
