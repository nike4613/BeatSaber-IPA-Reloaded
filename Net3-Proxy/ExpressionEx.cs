using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;

namespace Net3_Proxy
{
    public static class ExpressionEx
    {
        internal static ExpressionType ExprT(this ExpressionTypeEx ex)
            => (ExpressionType)ex;
        internal static ExpressionTypeEx ExprTEx(this ExpressionType ex)
            => (ExpressionTypeEx)ex;

        private static void Validate(Type type, bool allowByRef)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            TypeUtils.ValidateType(type, nameof(type), allowByRef, false);
            if (type == typeof(void))
                throw new ArgumentException("Argument cannot be of type void", nameof(type));
        }

		private static void RequiresCanWrite(Expression expression, string paramName)
		{
			if (expression == null)
			{
				throw new ArgumentNullException(paramName);
			}
			ExpressionType nodeType = expression.NodeType;
			if (nodeType != ExpressionType.MemberAccess)
			{
				if (nodeType == ExpressionType.Parameter)
					return;
			}
			else
			{
				MemberInfo member = ((MemberExpression)expression).Member;
				if (member is PropertyInfo propertyInfo)
				{
					if (propertyInfo.CanWrite)
					{
						return;
					}
				}
				else
				{
					FieldInfo fieldInfo = (FieldInfo)member;
					if (!fieldInfo.IsInitOnly && !fieldInfo.IsLiteral)
					{
						return;
					}
				}
			}
			throw new ArgumentException("Expression must be writeable", paramName);
		}

		public static void RequiresCanRead(Expression expression, string paramName, int idx = -1)
		{
			if (expression == null)
				throw new ArgumentNullException(TypeUtils.GetParamName(paramName, idx));
			ExpressionType nodeType = expression.NodeType;
			if (nodeType == ExpressionType.MemberAccess)
			{
				if (((MemberExpression)expression).Member is PropertyInfo propertyInfo && !propertyInfo.CanRead)
				{
					throw new ArgumentException("Expression must be readable", TypeUtils.GetParamName(paramName, idx));
				}
			}
		}

		public static VariableExpression Variable(Type type, string name = null)
        {
            Validate(type, false);
            return new VariableExpression(type, name);
        }

		public static AssignExpression Assign(Expression left, Expression right)
		{
			RequiresCanWrite(left, nameof(left));
			RequiresCanRead(right, nameof(right));
			TypeUtils.ValidateType(left.Type, nameof(left), true, true);
			TypeUtils.ValidateType(right.Type, nameof(right), true, true);
			if (!TypeUtils.AreReferenceAssignable(left.Type, right.Type))
				throw new ArgumentException($"Expression of type '{left.Type}' cannot be used for assignment to type '{right.Type}'");
			if (left.NodeType.ExprTEx() != ExpressionTypeEx.Variable)
				throw new NotSupportedException("Non-variable left hand operands to assignment is currently not supported");
			return new AssignExpression(left, right);
		}

		public static Expression Block(IEnumerable<VariableExpression> vars, params Expression[] body) => Block(vars, body);
		public static Expression Block(IEnumerable<VariableExpression> vars, IEnumerable<Expression> body)
		{ // this is probably terrible performance-wise when executing, but i'm not giving up BlockExpression damnit!
			var varSet = new HashSet<VariableExpression>(vars);
			var bodyArr = body.ToArray();
			var finalExprList = new Stack<(List<Expression> list, BlockParseStackInfo info)>();
			var remaining = bodyArr.Length;

			var varParams = new Dictionary<VariableExpression, ParameterExpression>(varSet.Count);

			finalExprList.Push((new List<Expression>(remaining), default(BlockParseStackInfo)));

			while (remaining > 0)
			{
				var (list, info) = finalExprList.Pop();
				var targetExpr = bodyArr[bodyArr.Length - remaining--];

				if (targetExpr.NodeType.ExprTEx() == ExpressionTypeEx.Assign)
				{
					var assign = (AssignExpression)targetExpr;
					var left = (VariableExpression)assign.Left;
					var right = assign.Right;

					var param = Expression.Parameter(left.Type, left.Name);

					finalExprList.Push((list, info));
					finalExprList.Push((new List<Expression>(remaining), new BlockParseStackInfo
					{
						ValueExpr = BlockVisitReplaceVariables(right, varParams), Param = param
					}));

					varParams.Add(left, param);
					continue;
				}

				list.Add(BlockVisitReplaceVariables(targetExpr, varParams));
				finalExprList.Push((list, info));
			}

			var funcType = typeof(Func<>);
			Expression topExpr = null;

			while (finalExprList.Count > 0)
			{
				var (list, info) = finalExprList.Pop();
				// there is an optimization opportunity for consecutive assignments, but it needs to make sure they don't depend on each other
				if (topExpr != null) list.Add(topExpr);


				Expression last = null;
				Type lastType = null;
				var rest = new List<Expression<Action>>(list.Count);

				if (list.Count == 0)
					list.Add(info.Param);

				for (int i = 0; i < list.Count; i++)
				{
					var expr = list[i];
					if (i + 1 == list.Count)
					{
						var ty = expr.Type;
						if (ty == typeof(void))
							rest.Add(Expression.Lambda<Action>(expr));
						else
						{
							lastType = ty;
							var func = funcType.MakeGenericType(ty);
							last = Expression.Lambda(func, expr);
						}
					}
					else
						rest.Add(Expression.Lambda<Action>(expr));
				}

				Expression topBody;

				if (rest.Count == 0)
					topBody = info.Param;
				else if (lastType != null)
				{
					var execSeq = ExecuteSequenceTyped.MakeGenericMethod(lastType);
					topBody = Expression.Call(null, execSeq, last, Expression.NewArrayInit(typeof(Action), rest.Cast<Expression>()));
				}
				else
					topBody = Expression.Call(null, ExecuteSequenceVoid, Expression.NewArrayInit(typeof(Action), rest.Cast<Expression>()));

				if (info.Param != null && info.ValueExpr != null)
					topExpr = Expression.Invoke(Expression.Lambda(topBody, info.Param), info.ValueExpr);
				else
					topExpr = topBody;
			}

			return topExpr;
		}

		/*
		 * {
		 *   Console.WriteLine("ho");
		 *   int i = 3;
		 *   Console.WriteLine(i);
		 *   i // return last
		 * }
		 * 
		 * ExecuteSequence<int>(
		 *   () => (i => 
		 *            ExecuteSequence<int>(
		 *              () => i, 
		 *              () => Console.WriteLine(i)
		 *            )
		 *         )(3), 
		 *   () => Console.WriteLine("ho")
		 * )
		 */

		private struct BlockParseStackInfo
		{
			public Expression ValueExpr;
			public ParameterExpression Param;
		}

		private static Expression BlockVisitReplaceVariables(Expression expr, Dictionary<VariableExpression, ParameterExpression> mapping)
		{
			var binary = expr as BinaryExpression;
			var unary = expr as UnaryExpression;
			switch (expr.NodeType)
			{
				case ExpressionType.Add:
					return Expression.Add(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.AddChecked:
					return Expression.AddChecked(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.And:
					return Expression.And(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.AndAlso:
					return Expression.AndAlso(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.ArrayIndex:
					return Expression.ArrayIndex(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.Coalesce:
					return Expression.Coalesce(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.Divide:
					return Expression.Divide(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.Equal:
					return Expression.Equal(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.ExclusiveOr:
					return Expression.ExclusiveOr(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.GreaterThan:
					return Expression.GreaterThan(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.GreaterThanOrEqual:
					return Expression.GreaterThanOrEqual(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.LeftShift:
					return Expression.LeftShift(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.LessThan:
					return Expression.LessThan(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.LessThanOrEqual:
					return Expression.LessThanOrEqual(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.Modulo:
					return Expression.Modulo(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.Multiply:
					return Expression.Multiply(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.MultiplyChecked:
					return Expression.MultiplyChecked(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.NotEqual:
					return Expression.NotEqual(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.Or:
					return Expression.Or(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.OrElse:
					return Expression.OrElse(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.Power:
					return Expression.Power(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.RightShift:
					return Expression.RightShift(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.Subtract:
					return Expression.Subtract(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.SubtractChecked:
					return Expression.SubtractChecked(BlockVisitReplaceVariables(binary.Left, mapping), BlockVisitReplaceVariables(binary.Right, mapping));
				case ExpressionType.ArrayLength:
					return Expression.ArrayLength(BlockVisitReplaceVariables(unary.Operand, mapping));
				case ExpressionType.Negate:
					return Expression.Negate(BlockVisitReplaceVariables(unary.Operand, mapping));
				case ExpressionType.UnaryPlus:
					return Expression.UnaryPlus(BlockVisitReplaceVariables(unary.Operand, mapping));
				case ExpressionType.NegateChecked:
					return Expression.NegateChecked(BlockVisitReplaceVariables(unary.Operand, mapping));
				case ExpressionType.Not:
					return Expression.Not(BlockVisitReplaceVariables(unary.Operand, mapping));
				case ExpressionType.TypeAs:
					return Expression.TypeAs(BlockVisitReplaceVariables(unary.Operand, mapping), unary.Type);
				case ExpressionType.Call:
					var callExpr = expr as MethodCallExpression;
					return Expression.Call(BlockVisitReplaceVariables(callExpr.Object, mapping),
						callExpr.Method, callExpr.Arguments.Select(a => BlockVisitReplaceVariables(a, mapping)));
				case ExpressionType.Conditional:
					var condExpr = expr as ConditionalExpression;
					return Expression.Condition(BlockVisitReplaceVariables(condExpr.Test, mapping),
						BlockVisitReplaceVariables(condExpr.IfTrue, mapping), BlockVisitReplaceVariables(condExpr.IfFalse, mapping));
				case ExpressionType.Constant: return expr; // constants should be unchanged
				case ExpressionType.Convert:
					return Expression.Convert(BlockVisitReplaceVariables(unary.Operand, mapping), unary.Type, unary.Method);
				case ExpressionType.ConvertChecked:
					return Expression.ConvertChecked(BlockVisitReplaceVariables(unary.Operand, mapping), unary.Type, unary.Method);
				case ExpressionType.Invoke:
					var invokeExpr = expr as InvocationExpression;
					return Expression.Invoke(BlockVisitReplaceVariables(invokeExpr.Expression, mapping),
						invokeExpr.Arguments.Select(e => BlockVisitReplaceVariables(e, mapping)));
				case ExpressionType.Lambda:
					var lambdaExpr = expr as LambdaExpression;
					return Expression.Lambda(lambdaExpr.Type, BlockVisitReplaceVariables(lambdaExpr.Body, mapping), lambdaExpr.Parameters);
				case ExpressionType.ListInit:
					var listInitExpr = expr as ListInitExpression;
					return Expression.ListInit((NewExpression)BlockVisitReplaceVariables(listInitExpr.NewExpression, mapping),
						listInitExpr.Initializers.Select(i => i.AddMethod).First(),
						listInitExpr.Initializers.SelectMany(i => i.Arguments)
							.Select(e => BlockVisitReplaceVariables(e, mapping)));
				case ExpressionType.MemberAccess:
					var memberExpr = expr as MemberExpression;
					return Expression.MakeMemberAccess(BlockVisitReplaceVariables(memberExpr.Expression, mapping), memberExpr.Member);
				case ExpressionType.MemberInit:
					var memberInitExpr = expr as MemberInitExpression;
					return Expression.MemberInit((NewExpression)BlockVisitReplaceVariables(memberInitExpr.NewExpression, mapping),
						memberInitExpr.Bindings);
				case ExpressionType.New:
					var newExpr = expr as NewExpression;
					return Expression.New(newExpr.Constructor,
						newExpr.Arguments.Select(e => BlockVisitReplaceVariables(e, mapping)),
						newExpr.Members);
				case ExpressionType.NewArrayInit:
					var newArrayInitExpr = expr as NewArrayExpression;
					return Expression.NewArrayInit(newArrayInitExpr.Type,
						newArrayInitExpr.Expressions.Select(e => BlockVisitReplaceVariables(e, mapping)));
				case ExpressionType.NewArrayBounds:
					var newArrayBoundsExpr = expr as NewArrayExpression;
					return Expression.NewArrayBounds(newArrayBoundsExpr.Type,
						newArrayBoundsExpr.Expressions.Select(e => BlockVisitReplaceVariables(e, mapping)));
				case ExpressionType.Parameter: return expr; // like constant
				case ExpressionType.Quote:
					return Expression.Quote(BlockVisitReplaceVariables(unary.Operand, mapping));
				case ExpressionType.TypeIs:
					var typeIsExpr = expr as TypeBinaryExpression;
					return Expression.TypeIs(BlockVisitReplaceVariables(typeIsExpr.Expression, mapping), typeIsExpr.TypeOperand);
				default:
					switch (expr.NodeType.ExprTEx())
					{
						case ExpressionTypeEx.Variable:
							var varExpr = expr as VariableExpression;
							if (mapping.TryGetValue(varExpr, out var paramExpr))
								return paramExpr;
							else
								return varExpr; // not in scope in the current context, might be later
						case ExpressionTypeEx.Assign:
							throw new InvalidOperationException("Assign expression must appear directly inside a block expression");
						default:
							throw new ArgumentException($"Unhandled expression type '{expr.NodeType}'");
					}
			}
		}

		private static readonly MethodInfo ExecuteSequenceVoid = typeof(ExpressionEx).GetMethod(nameof(ExecuteSequence), 
			BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(Action[]) }, null);
		private static void ExecuteSequence(params Action[] exec)
		{
			foreach (var act in exec) act();
		}
		private static readonly MethodInfo ExecuteSequenceTyped = typeof(ExpressionEx).GetMethod(nameof(ExecuteSequence),
			BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(Func<>), typeof(Action[]) }, null);
		private static T ExecuteSequence<T>(Func<T> last, params Action[] first)
		{
			ExecuteSequence(first);
			return last();
		}
    }

    internal enum ExpressionTypeEx 
    {
        Variable = 46,
        Assign = 47
    }

    public class VariableExpression : Expression, IEquatable<VariableExpression>
    {
        public string Name { get; }

        internal VariableExpression(Type varType, string name) : base(ExpressionTypeEx.Variable.ExprT(), varType)
            => Name = name;

		public bool Equals(VariableExpression other)
			=> Name == other.Name && Type == other.Type;
	}

	public class AssignExpression : Expression
	{
		public Expression Left { get; }
		public Expression Right { get; }

		internal AssignExpression(Expression left, Expression right) : base(ExpressionTypeEx.Assign.ExprT(), left.Type)
		{
			Left = left;
			Right = right;
		}
	}
}
