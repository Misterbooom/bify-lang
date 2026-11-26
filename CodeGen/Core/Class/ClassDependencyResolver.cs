using System;
using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Core.Class;

public class ClassDependencyResolver(CodeGenContext ctx) : CodeGenBase(ctx)
{
    private enum VisitState
    {
        VISITING,
        VISITED
    }

    private readonly Dictionary<string, ClassInfo> _classes = [];
    private readonly Dictionary<string, string> _inheritanceMap = [];
    private readonly Dictionary<string, VisitState> _states = [];
    private readonly Dictionary<string, string> _modulePaths = [];
    private readonly List<ClassInfo> _compilationOrder = [];
    private readonly Dictionary<string, List<string>> _invertedMap = [];
    private int _typeId;

    public ClassInfo[] Resolve(IEnumerable<AstModule> modules)
    {
        _classes.Clear();
        _inheritanceMap.Clear();
        _states.Clear();
        _invertedMap.Clear();
        _modulePaths.Clear();
        _compilationOrder.Clear();

        foreach (var astModule in modules)
        {
            _modulePaths[astModule.Name] = astModule.ModulePath;
            using (_ctx.EnterModule(astModule.Name, astModule.ModulePath))
            {
                CollectClasses(astModule);
            }
        }

        BifyDebug.Log(string.Join(
            Environment.NewLine,
            _invertedMap.Select(x => $"{x.Key ?? "noParent"}: [{string.Join(", ", x.Value)}]")
        ));
        VerifyInheritanceMap();

        foreach (var mangledName in _classes.Keys)
        {
            Visit(mangledName);
        }

        if (_invertedMap.TryGetValue(string.Empty, out var noParent))
        {
            foreach (var root in noParent)
            {
                int minId = NextId();
                if (_invertedMap.TryGetValue(root, out var list))
                {
                    AssignTypeIds(list);
                }

                int maxId = GetId();
                var classInfo = _compilationOrder.Single(i => i.MangledName == root);
                classInfo.MinType = minId;
                classInfo.MaxType = maxId;
            }
        }

        BifyDebug.Log(string.Join("\n", _compilationOrder));


        return _compilationOrder.ToArray();
    }

    private int NextId() => ++_typeId;
    private int GetId() => _typeId;

    private void CollectClasses(AstModule module)
    {
        foreach (AstNode node in module.ChildNodes)
        {
            if (node is not AstClass classNode)
                continue;

            _ctx.CurrentLine = classNode.LineNumber;
            string rawClassName = classNode.NameNode.Token.Value;
            string mangledName = NameMangler.GenerateTypeName(module.Name, rawClassName);

            if (_classes.ContainsKey(mangledName))
            {
                Error($"Class '{rawClassName}' already exists in module '{module.Name}'.", DiagnosticKind.NAME,
                    classNode);
                continue;
            }

            try
            {
                string mangledParentName = ResolveParentName(_ctx, classNode.ParentNode, module.Name);
                var classInfo = new ClassInfo(rawClassName, classNode, module.Name, mangledName);


                _classes.Add(mangledName, classInfo);
                _inheritanceMap.Add(mangledName, mangledParentName);
                if (!_invertedMap.TryGetValue(mangledParentName, out var list))
                {
                    list = [];
                    _invertedMap[mangledParentName] = list;
                }

                list.Add(mangledName);
            }
            catch (BifyCompilationException)
            {
            }
        }
    }

    public static string ResolveParentName(CodeGenContext ctx, AstNode? node, string currentModuleName)
    {
        return node switch
        {
            null => string.Empty,
            AstIdentifier id => NameMangler.GenerateTypeName(currentModuleName, id.Name),
            AstMemberAccess { Left: AstIdentifier moduleId, Right: AstIdentifier classId }
                => NameMangler.GenerateTypeName(moduleId.Name, classId.Name),
            AstMemberAccess
                => ctx.Fail<string>("Invalid parent class reference.", DiagnosticKind.SYNTAX, node),
            _ => throw new NotSupportedException($"'{node.GetType().Name}' is not supported as a parent class node.")
        };
    }

    private void VerifyInheritanceMap()
    {
        foreach (var (mangledClassName, mangledParentName) in _inheritanceMap)
        {
            if (string.IsNullOrEmpty(mangledParentName))
                continue;

            if (!_classes.ContainsKey(mangledParentName))
            {
                var info = _classes[mangledClassName];
                string path = _modulePaths.GetValueOrDefault(info.ModuleName, "");
                using (_ctx.EnterModule(info.ModuleName, path))
                {
                    Fail(
                        $"Class '{mangledClassName}' inherits from unknown class '{mangledParentName}'.",
                        DiagnosticKind.TYPE,
                        info.ClassNode
                    );
                }
            }
        }
    }

    private void Visit(string mangledClassName)
    {
        if (_states.TryGetValue(mangledClassName, out var state))
        {
            if (state == VisitState.VISITING)
            {
                var info = _classes[mangledClassName];
                string path = _modulePaths.GetValueOrDefault(info.ModuleName, "");
                using (_ctx.EnterModule(info.ModuleName, path))
                {
                    Fail(
                        $"Circular inheritance detected at class '{mangledClassName}'.",
                        DiagnosticKind.CIRCULAR_INHERITANCE,
                        info.ClassNode
                    );
                }
            }

            if (state == VisitState.VISITED)
                return;
        }

        _states[mangledClassName] = VisitState.VISITING;

        string mangledParentName = _inheritanceMap[mangledClassName];
        if (!string.IsNullOrEmpty(mangledParentName))
        {
            Visit(mangledParentName);
        }

        _states[mangledClassName] = VisitState.VISITED;
        _compilationOrder.Add(_classes[mangledClassName]);
    }

    private void AssignTypeIds(List<string> childs)
    {
        foreach (var child in childs)
        {
            int minId = NextId();
            if (_invertedMap.TryGetValue(child, out var list))
            {
                AssignTypeIds(list);
            }

            int maxId = GetId();
            var classInfo = _compilationOrder.Single(i => i.MangledName == child);
            classInfo.MinType = minId;
            classInfo.MaxType = maxId;
        }
    }

    public override string ToString()
    {
        return string.Join(
            Environment.NewLine,
            _compilationOrder.ConvertAll(c =>
                $"{c.ModuleName}.{c.Name}:{_inheritanceMap[NameMangler.GenerateTypeName(c.ModuleName,c.Name)]}")
        );
    }
}
