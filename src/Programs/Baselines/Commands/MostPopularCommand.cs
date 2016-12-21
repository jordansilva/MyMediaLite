// Copyright (C) 2012 Zeno Gantner
//
// This file is part of MyMediaLite.
//
// MyMediaLite is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MyMediaLite is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with MyMediaLite.  If not, see <http://www.gnu.org/licenses/>.
//
using System;
using System.Linq;
using MyMediaLite.ItemRecommendation;
using System.Collections.Generic;
using MyMediaLite;
using MyMediaLite.Data;
using MyMediaLite.IO;

namespace Baselines.Commands
{

	public class ItemKNNCommand : Command
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger (System.Reflection.MethodBase.GetCurrentMethod ().DeclaringType);

		private static uint [] K_PARAMETERS = { 5, 10, 20, 30, 50, 80, 100, 200, 500, 1000 };

		public ItemKNNCommand (string training, string test) : base (training, test, typeof (ItemKNN))
		{
			((ItemKNN)Recommender).K = 80; //default
			((ItemKNN)Recommender).Correlation = MyMediaLite.Correlation.BinaryCorrelationType.Cosine;
			((ItemKNN)Recommender).Weighted = false;
			((ItemKNN)Recommender).Alpha = 0.5f;
			((ItemKNN)Recommender).Q = 1;
		}

		protected override IPosOnlyFeedback LoadPositiveFeedback (string path, ItemDataFileFormat file_format)
		{
			IPosOnlyFeedback feedback = ItemData.Read (path,
			                                           new IdentityMapping (),
													   new IdentityMapping (),
													   file_format == ItemDataFileFormat.IGNORE_FIRST_LINE);
			return feedback;
		}

		public override void Tunning ()
		{
			if (Feedback == null || Feedback.Count == 0)
				throw new Exception ("Training data can not be null");

			if (Test == null || Test.Count == 0)
				throw new Exception ("Test data can not be null");

			log.Info ("Tunning K parameter");
			TunningK ();
		}

		void TunningK ()
		{
			var mrr_tunning = new List<Tuple<uint, double>> ();
			//uint k = 80;
			foreach (var k in K_PARAMETERS) {
				QueryResult result = Train (k);
				double mrr = result.GetMetric ("MRR");
				Log (k, mrr);
				mrr_tunning.Add (Tuple.Create (k, mrr));
			}

			mrr_tunning = mrr_tunning.OrderByDescending (x => x.Item2).ToList ();
			K_PARAMETERS = mrr_tunning.Select (x => x.Item1).Distinct ().Take (3).ToArray ();
			log.Debug (string.Format ("Bests K parameters: {0}", string.Join (",", K_PARAMETERS)));
		}

		QueryResult Train (uint k, float alpha = 0.5f, bool weighted = false)
		{
			bool evaluate = true;
			QueryResult result = null;
			while (evaluate) {
				try {
					CreateModel (typeof (ItemKNN));
					((ItemKNN)Recommender).Feedback = Feedback;
					((ItemKNN)Recommender).K = k;
					((ItemKNN)Recommender).Alpha = alpha;
					((ItemKNN)Recommender).Weighted = weighted;

					TimeSpan t = Wrap.MeasureTime (delegate () {
						Train ();
						result = Evaluate ();

					});

					Console.WriteLine ("Training and Evaluate model: {0} seconds", t.TotalSeconds);
					evaluate = false;
				} catch (Exception ex) {
					Console.WriteLine (string.Format ("Exception {0}:", ex.Message));
					evaluate = true;
				}
			}

			return result;
		}

		void Log (uint k, double metric)
		{
			log.Info (string.Format ("k={0}\t\t-\t\tMRR = {1}", k, metric));
		}

		public override void Evaluate (string filename)
		{
			throw new NotImplementedException ();
		}
	}
}
