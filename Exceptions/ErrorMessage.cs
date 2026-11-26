namespace BifyLang.Exceptions
{
    public static class ErrorMessage
    {
        public static string InvalidFunctionType() => "expected identifier";
        public static string InvalidForLoopStructure() => "invalid 'for' header";
        public static string InitStatementIsRequired() => "missing 'for' init";
        public static string ConditionIsRequired() => "missing condition";
        public static string IncrementStatementIsRequired() => "missing 'for' increment";
        public static string InvalidInitNode() => "invalid 'for' init";
        public static string InvalidInitNodeType() => "expected declaration or assignment in 'for' init";
        public static string InvalidConditionNode() => "invalid 'for' condition";
        public static string InvalidConditionNodeType() => "invalid condition expression";
        public static string InvalidIncrementNode() => "invalid 'for' increment";
        public static string InvalidIncrementNodeType() => "expected assignment or unary op in 'for' increment";
        public static string InvalidOperandType(string operand) => $"invalid operand '{operand}'";
        public static string UnmatchedOpeningParenthesis() => "unclosed '('";
        public static string UnmatchedToken(string open, string close) => $"expected '{close}' to match '{open}'";
        public static string UnmatchedClosingParenthesis() => "unmatched ')'";

        public static string UnmatchedOpeningBrace() => "unclosed '{'";

        public static string UnmatchedClosingBrace() => "unmatched '}'";
        public static string UnmatchedOpeningBracket() => "unclosed '['";
        public static string UnmatchedClosingBracket() => "unmatched ']'";
        public static string ElseWithoutMatchingIf() => "'else' without 'if'";

        public static string ElseIfWithoutMatchingIf() => "'else if' without 'if'";

        public static string ElseIfCannotFollowElseDirectly() => "'else if' after 'else'";

        public static string InvalidVariableDeclaration() => "expected variable declaration in 'for' init";
        public static string InvalidIndexTarget() => "cannot index expression";

        public static string MissingCloseQuotationMark() => "unterminated string literal";

        public static string NotEnoughOperands(string op) => $"missing operand for '{op}'";

        public static string InvalidTypeForOperation(string op, string left, string right) => $"invalid operands '{left}' and '{right}' for '{op}'";

        public static string DivisionByZero() => "division by zero";

        public static string IndexOfArrayTypeError() => "array index must be 'int'";
        public static string ArrayIsEmpty() => "empty array";
        public static string InvalidIndex(int index, int length) => $"index {index} out of bounds for length {length}";
        public static string MissingOperandAfterUnaryOperator(string operatorSymbol) => $"missing operand for '{operatorSymbol}'";
        public static string OperandMustBeIdentifierAfterUnaryOperator(string operatorSymbol) => $"expected identifier for '{operatorSymbol}'";

        public static string UnexpectedToken(string token) => $"unexpected token '{token}'";
        public static string ExpectedValueAfterAssignment() => "expected expression after '='";
        public static string ExpectedTokenNotFound(string tokenType) => $"expected '{tokenType}'";

        public static string InvalidFunctionDeclaration() => "invalid function declaration";
        public static string ExpectedFunctionName() => "expected function name";
        public static string InvalidParameter(string parameterType) => $"invalid parameter '{parameterType}'";
        public static string InvalidIndexExpression() => "invalid index expression";
        public static string OperationNotSupported(string operation, string objectName) => $"unsupported '{operation}' for '{objectName}'";

        public static string InvalidVariableName(string var) => $"expected identifier, found '{var}'";
        public static string EmptyValueAssigned() => "expected expression after '='";
        public static string MissingAssignmentOperator() => "expected '='";

        public static string InvalidSyntaxInAssignment() => "invalid assignment";

        public static string ExpectedTypeOfVariable() => "missing variable type";

        public static string InvalidVariableType(string value) => $"invalid type '{value}'";
        public static string InvalidFunctionReturnType(string type, string targetType) => $"expected return type '{targetType}', found '{type}'";
        public static string UndefindedVariable(string name) => $"undeclared identifier '{name}'";
        public static string ConditionIsRequiredInSwitch() => "missing 'switch' condition";
        public static string MultipleDefaultInSwitch() => "duplicate 'default' in 'switch'";
        public static string ExpectedColonAfterCase() => "expected ':' after 'case'";
        public static string ExpectedColonAfterDefault() => "expected ':' after 'default'";
        public static string InvalidSwitchBodyToken(string token) => $"unexpected token '{token}' in 'switch' body";
    }
}