using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EntityQueryProviderDemo
{
    public class EntityContext<T> : IQueryable<T>, IQueryProvider
    {
        public class EntityCollection<T> : IEnumerable<T>
        {
            private EntityContext<T> _context = null;

            #region IEnumerable<T> Members

            public IEnumerator<T> GetEnumerator()
            {
                return new EntityEnumerator<T>(this);
            }

            #endregion

            #region IEnumerable Members

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            #endregion            

            public EntityCollection(EntityContext<T> context)
            {
                _context = context;
            }

            public class EntityEnumerator<T> : IEnumerator<T>
            {
                private EntityCollection<T> _context = null;
                private DbDataReader _reader = null;
                private T _current;

                #region IEnumerator<T> Members

                public T Current
                {
                    get
                    {
                        return _current;
                    }
                }

                #endregion

                #region IDisposable Members

                public void Dispose()
                {
                    if (_reader != null)
                        _reader.Dispose();
                    if (_context._context._command.Connection != null && _context._context._command.Connection.State == System.Data.ConnectionState.Open)
                        _context._context._command.Connection.Close();
                }

                #endregion

                #region IEnumerator Members

                object System.Collections.IEnumerator.Current
                {
                    get
                    {
                        return _current;
                    }
                }

                public bool MoveNext()
                {
                    if (_reader == null)
                        Reset();
                    _current = BuildObject();
                    return _current != null;
                }

                public void Reset()
                {
                    if (_reader != null)
                        _reader.Dispose();
                    if (_context._context._command.Connection.State == System.Data.ConnectionState.Closed)
                        _context._context._command.Connection.Open();
                    _reader = _context._context._command.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                }

                #endregion

                private T BuildObject()
                {
                    if (_reader.Read())
                    {
                        object obj = Activator.CreateInstance(typeof(T), false);
                        foreach (PropertyInfo pi in obj.GetType().GetProperties())
                        {
                            if (!_reader.IsDBNull(_reader.GetOrdinal(pi.Name)))
                            {
                                object value = _reader.GetValue(_reader.GetOrdinal(pi.Name));
                                pi.SetValue(obj, value, null);
                            }
                        }
                        return (T)obj;
                    }
                    return default(T);
                }

                public EntityEnumerator(EntityCollection<T> context)
                {
                    _context = context;
                }
            }
        }

        private Expression _expression;
        private DbCommand _command = null;
        private string _tableName = string.Empty;

        public Expression Expression => Expression.Constant(this);

        public Type ElementType => typeof(T);

        public IQueryProvider Provider => this;

        public IQueryable CreateQuery(Expression expression)
        {
            return CreateQuery<T>(expression);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            _expression = expression;
            return (IQueryable<TElement>)this;
        }

        public object Execute(Expression expression)
        {
            return Execute<IEnumerator<T>>(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            MethodCallExpression methodcall = _expression as MethodCallExpression;
            StringBuilder sb = new StringBuilder();


            if (methodcall.Method.Name == "Where")
            {
                sb.Append($"SELECT * FROM {_tableName}");
                sb.Append(" WHERE  ");
            }

            if (methodcall.Method.Name == "Select")
                sb.Append($"SELECT * FROM {_tableName} ");

            ProcessExpression(methodcall.Arguments[1], sb);
            _command.CommandText = sb.ToString();
            EntityCollection<T> result = new EntityCollection<T>(this);
            return (TResult)result.GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return (this as IQueryable).Provider.Execute<IEnumerator<T>>(_expression);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return (IEnumerator<T>)(this as IQueryable).GetEnumerator();
        }
        private void ProcessExpression(Expression expression, StringBuilder sb)
        {
            if (expression.NodeType == ExpressionType.Equal)
            {
                ProcessExpression(((BinaryExpression)expression).Left, sb);
                sb.Append(" = ");
                ProcessExpression(((BinaryExpression)expression).Right, sb);

            }

            if (expression.NodeType == ExpressionType.LessThan)
            {
                BinaryExpression expr = (BinaryExpression)expression;
                ProcessExpression(expr.Left, sb);
                sb.Append("<");
                ProcessExpression(expr.Right, sb);
            }

            if (expression.NodeType == ExpressionType.GreaterThan)
            {
                BinaryExpression expr = (BinaryExpression)expression;
                ProcessExpression(expr.Left, sb);
                sb.Append(" >");
                ProcessExpression(expr.Right, sb);
            }

            if (expression.NodeType == ExpressionType.Or)
            {
                BinaryExpression expr = (BinaryExpression)expression;
                ProcessExpression(expr.Left, sb);
                sb.Append(" Or ");
                ProcessExpression(expr.Right, sb);
            }

            if (expression.NodeType == ExpressionType.OrElse)
            {
                BinaryExpression expr = (BinaryExpression)expression;
                ProcessExpression(expr.Left, sb);
                sb.Append(" Or ");
                ProcessExpression(expr.Right, sb);
            }

            if (expression is UnaryExpression)
            {
                UnaryExpression uExp = expression as UnaryExpression;
                ProcessExpression(uExp.Operand, sb);
            }
            else if (expression is LambdaExpression)
            {
                ProcessExpression(((LambdaExpression)expression).Body, sb);
            }
            else if (expression is MethodCallExpression)
            {
                sb.Append(" ");
                sb.Append(((MethodCallExpression)expression).Method.DeclaringType.ToString() + ".");
                sb.Append(((MethodCallExpression)expression).Method.Name);
                sb.Append("(");
                foreach (object param in ((MethodCallExpression)expression).Arguments)
                {
                    if (param is Expression)
                        ProcessExpression((Expression)param, sb);
                    else
                        sb.Append(param.ToString());
                    sb.Append(",");
                }
                sb = sb.Remove(sb.Length - 1, 1);
                sb.Append(") ");
            }
            else if (expression is MemberExpression)
            {
                sb.Append(((MemberExpression)expression).Member.Name);
            }
            else if (expression is ConstantExpression)
            {
                if (((ConstantExpression)expression).Value is Expression)
                    ProcessExpression((Expression)((ConstantExpression)expression).Value, sb);
                object value = ((ConstantExpression)expression).Value;
                if (value is string)
                    sb.Append("'");
                sb.Append(((ConstantExpression)expression).Value.ToString());
                if (value is string)
                    sb.Append("'");
            }
        }

        public EntityContext(string tableName, DbCommand command)
        {
            _tableName = tableName;
            _command = command;
        }
    }
}
