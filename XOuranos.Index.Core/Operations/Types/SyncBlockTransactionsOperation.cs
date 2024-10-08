using System.Collections.Generic;
using XOuranos.Consensus.TransactionInfo;
using XOuranos.Index.Core.Client.Types;

namespace XOuranos.Index.Core.Operations.Types
{
   /// <summary>
   /// The sync block info.
   /// </summary>
   public class SyncBlockTransactionsOperation
   {
      /// <summary>
      /// Gets or sets the block info.
      /// </summary>
      public BlockInfo BlockInfo { get; set; }

      /// <summary>
      /// Gets or sets the transactions.
      /// </summary>
      public IEnumerable<Transaction> Transactions { get; set; }
   }
}
