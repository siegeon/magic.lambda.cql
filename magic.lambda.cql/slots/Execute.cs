﻿/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System.Linq;
using System.Collections;
using System.Threading.Tasks;
using Cassandra;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;

namespace magic.lambda.cql.slots
{
    /// <summary>
    /// [cql.execute] slot, for executing som CQL towards a keyspace and returning the result of the invocation to caller.
    /// </summary>
    [Slot(Name = "cql.execute")]
    public class Execute : ISlot, ISlotAsync
    {
        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler used to raise the signal.</param>
        /// <param name="input">Arguments to your slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            SignalAsync(signaler, input).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler used to raise the signal.</param>
        /// <param name="input">Arguments to your slot.</param>
        /// <returns>An awaitable task.</returns>
        public async Task SignalAsync(ISignaler signaler, Node input)
        {
            var cql = input.GetEx<string>();
            var args = input.Children.ToDictionary(x => x.Name, x => x.GetEx<object>());
            input.Clear();
            input.Value = null;
            var rowSet = await signaler.Peek<ISession>("cql.connect").ExecuteAsync(new SimpleStatement(args, cql));
            foreach (var idx in rowSet)
            {
                var cur = new Node(".");
                for (var idxNo = 0; idxNo < idx.Length; idxNo++)
                {
                    var value = Convert(idx[idxNo]);
                    if (value is Node node)
                        cur.Add(new Node(rowSet.Columns[idxNo].Name, null, node.Children.ToList()));
                    else
                        cur.Add(new Node(rowSet.Columns[idxNo].Name, value));
                }
                input.Add(cur);
            }
        }

        #region [ -- Private helper methods -- ]

        /*
         * Helper method to convert raw objects returned from NoSQL storage to something
         * we can intelligently handle in Hyperlambda.
         */
        static object Convert(object value)
        {
            if (value == null)
                return null;
            if (value is IDictionary dictionary)
            {
                var result = new Node();
                foreach (string idx in dictionary.Keys)
                {
                    result.Add(new Node(idx, dictionary[idx]));
                }
                return result;
            }
            return value;
        }

        #endregion
    }
}
