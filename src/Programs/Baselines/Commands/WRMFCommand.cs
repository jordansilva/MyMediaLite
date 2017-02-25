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
using System.Collections.Generic;
using System.Linq;
using Mono.Options;
using MyMediaLite;
using MyMediaLite.Data;
using MyMediaLite.IO;
using MyMediaLite.ItemRecommendation;

namespace Baselines.Commands
{
	public class WRMFCommand : Command
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger (System.Reflection.MethodBase.GetCurrentMethod ().DeclaringType);
		private static double [] REGULARIZATION = { 0.01, 0.015, 0.03, 0.04, 0.05, 0.07, 0.1 };
		private static uint [] LATENT_FACTORS = { 5, 10, 20, 30, 50, 100 };

		public WRMFCommand (string training, string test) : base (training, test, typeof (WRMF))
		{
			((WRMF)Recommender).NumFactors = 10;
			((WRMF)Recommender).NumIter = 25;
		}

		protected override void Init ()
		{
			base.Init ();
			((WRMF)Recommender).Feedback = Feedback;
		}

		public override void Tunning ()
		{
			if (Feedback == null || Feedback.Count == 0)
				throw new Exception ("Training data can not be null");

			if (Test == null || Test.Count == 0)
				throw new Exception ("Test data can not be null");

			log.Info ("Tunning Regularization parameter");
			TunningRegularization ();

			log.Info ("Tunning Latent Factors parameter");
			TunningLatentFactors ();

			//Train (5, 1, 0.015);
			//Train (5, 1, 0.1);
			//Train (5, 1, 0.05);
			//Train (10, 1, 0.1);
		}

		void TunningRegularization ()
		{
			var num_factors = ((WRMF)Recommender).NumFactors;
			var alpha = ((WRMF)Recommender).Alpha;

			var mrr_tunning = new List<Tuple<double, double>> ();

			foreach (var reg in REGULARIZATION) {
				QueryResult result = Train (num_factors, alpha, reg);
				double mrr = result.GetMetric ("MRR");
				Log (num_factors, reg, alpha, mrr);
				mrr_tunning.Add (Tuple.Create (reg, mrr));
			}

			mrr_tunning = mrr_tunning.OrderByDescending (x => x.Item2).ToList ();
			REGULARIZATION = mrr_tunning.Select (x => x.Item1).Take (3).ToArray ();
			log.Debug (string.Format ("REGULARIZATION was changed to: {0}", string.Join (",", REGULARIZATION)));
		}

		void TunningLatentFactors ()
		{
			var mrr_tunning = new List<Tuple<uint, double>> ();
			var alpha = ((WRMF)Recommender).Alpha;

			foreach (var num_factors in LATENT_FACTORS) {
				foreach (var reg in REGULARIZATION) {
					QueryResult result = Train (num_factors, alpha, reg);
					double mrr = result.GetMetric ("MRR");
					Log (num_factors, reg, alpha, mrr);
					mrr_tunning.Add (Tuple.Create (num_factors, mrr));
				}
			}

			mrr_tunning = mrr_tunning.OrderByDescending (x => x.Item2).ToList ();
			LATENT_FACTORS = mrr_tunning.Select (x => x.Item1).Take (3).ToArray ();
			log.Debug (string.Format ("LATENT FACTORS was changed to: {0}", string.Join (",", LATENT_FACTORS)));
		}

		QueryResult Train (uint num_factors, double alpha, double regularization)
		{
			bool evaluate = true;
			QueryResult result = null;
			while (evaluate) {
				try {
					CreateModel (typeof (WRMF));
					((WRMF)Recommender).NumIter = 25;
					((WRMF)Recommender).NumFactors = num_factors;
					((WRMF)Recommender).Alpha = alpha;
					((WRMF)Recommender).Regularization = regularization;
					((WRMF)Recommender).Feedback = Feedback;

					TimeSpan t = Wrap.MeasureTime (delegate () {
						Train ();
						result = Evaluate ();
					});

					Console.WriteLine ("Training and Evaluate model: {0} seconds", t.TotalSeconds);
					string filename = string.Format ("WRMF-n{0}-a{1}-r{2}", num_factors, alpha, regularization);
					SaveModel (string.Format ("output/model/{0}.model", filename));
					MyMediaLite.Helper.Utils.SaveRank (filename, result);

					evaluate = false;
				} catch (Exception ex) {
					Console.WriteLine (ex.Message);
					evaluate = true;
				}
			}

			return result;
		}

		void Log (uint num_factors, double regularization, double alpha, double metric)
		{
			log.Info (string.Format ("n={0}\tr={1}\ta={2}\t\t-\t\tMRR = {3}", num_factors,
									 regularization, alpha, metric));
		}

		public override void Evaluate (string filename)
		{
			if (!string.IsNullOrEmpty (filename)) {
				Console.WriteLine ("Loading test data");
				if (string.IsNullOrEmpty (path_test) || !path_test.Equals (filename, StringComparison.InvariantCultureIgnoreCase)) {
					Test = LoadTest (filename);
					TestFeedback = LoadPositiveFeedback (filename, ItemDataFileFormat.IGNORE_FIRST_LINE);
				}

				var result = Evaluate ();
				MyMediaLite.Helper.Utils.SaveRank ("ranked-items.rank", result);
				Log (((WRMF)Recommender).NumFactors, ((WRMF)Recommender).Regularization,
					 ((WRMF)Recommender).Alpha, result.GetMetric ("MRR"));
			}
		}

		public override void SetupOptions (string [] args)
		{
			base.SetupOptions (args);

			var options = new OptionSet {
				{ "num-factors=", v => ((WRMF)Recommender).NumFactors = uint.Parse(v)},
				{ "regularization=", v => ((WRMF)Recommender).Regularization = double.Parse(v)}};

			options.Parse (args);

			log.Info ("Parameters configured!");
			log.Info ("Num Factors: " + ((WRMF)Recommender).NumFactors);
			log.Info ("Regularization: " + ((WRMF)Recommender).Regularization);
		}
	}
}
