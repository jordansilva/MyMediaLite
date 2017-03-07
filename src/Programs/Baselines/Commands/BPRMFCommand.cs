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
using Mono.Options;
using MyMediaLite.IO;

namespace Baselines.Commands
{

	public class BPRMFCommand : Command
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger (System.Reflection.MethodBase.GetCurrentMethod ().DeclaringType);

		private static float [] LEARN_RATE = { 0.001f, 0.005f, 0.01f, 0.02f, 0.03f, 0.04f, 0.05f, 0.06f, 0.07f, 0.1f };
		private static uint [] LATENT_FACTORS = { 5, 10, 20, 30, 50, 100, 500, 1000 };
		private static float [] REGULARIZATION = { 0.0025f, 0.01f, 0.03f, 0.04f, 0.05f, 0.06f, 0.07f, 0.1f };

		public BPRMFCommand (string training, string test) : base (training, test, typeof (MultiCoreBPRMF))
		{
			((MultiCoreBPRMF)Recommender).BiasReg = 0;
			((MultiCoreBPRMF)Recommender).NumFactors = 10;
			((MultiCoreBPRMF)Recommender).RegU = 0.0025f;
			((MultiCoreBPRMF)Recommender).RegI = 0.0025f;
			((MultiCoreBPRMF)Recommender).RegJ = 0.00025f;
			((MultiCoreBPRMF)Recommender).NumIter = 25;
			((MultiCoreBPRMF)Recommender).LearnRate = 0.05f;
			((MultiCoreBPRMF)Recommender).UniformUserSampling = true;
			((MultiCoreBPRMF)Recommender).WithReplacement = false;
			((MultiCoreBPRMF)Recommender).UpdateJ = true;
		}

		protected override void Init ()
		{
			base.Init ();
			((MultiCoreBPRMF)Recommender).Feedback = Feedback;
		}

		public override void Tunning ()
		{
			if (Feedback == null || Feedback.Count == 0)
				throw new Exception ("Training data can not be null");

			if (Test == null || Test.Count == 0)
				throw new Exception ("Test data can not be null");

			log.Info ("Tunning Regularization parameter");
			TunningRegularization ();

			REGULARIZATION = new float [] { 0.01f, 0.03f, 0.0025f };
			log.Info ("Tunning Latent Factors parameter");
			TunningLatentFactors ();

			log.Info ("Tunning Learning Rate parameter");
			TunningLearningRate ();
		}

		void TunningRegularization ()
		{
			var num_factors = ((MultiCoreBPRMF)Recommender).NumFactors;
			var learnrate = ((MultiCoreBPRMF)Recommender).LearnRate;

			var mrr_tunning = new List<Tuple<float, double>> ();

			foreach (var reg in REGULARIZATION) {
				QueryResult result = Train (num_factors, learnrate, reg);
				double mrr = result.GetMetric ("MRR");
				Log (num_factors, reg, learnrate, mrr);
				mrr_tunning.Add (Tuple.Create (reg, mrr));
			}

			mrr_tunning = mrr_tunning.OrderByDescending (x => x.Item2).ToList ();
			REGULARIZATION = mrr_tunning.Select (x => x.Item1).Distinct ().Take (3).ToArray ();
			log.Debug (string.Format ("REGULARIZATION was changed to: {0}", string.Join (",", REGULARIZATION)));
		}

		void TunningLatentFactors ()
		{
			var mrr_tunning = new List<Tuple<uint, double>> ();
			var learnrate = ((MultiCoreBPRMF)Recommender).LearnRate;

			foreach (var reg in REGULARIZATION) {
				foreach (var num_factors in LATENT_FACTORS) {
					QueryResult result = Train (num_factors, learnrate, reg);
					double mrr = result.GetMetric ("MRR");
					Log (num_factors, reg, learnrate, mrr);
					mrr_tunning.Add (Tuple.Create (num_factors, mrr));
				}
			}

			mrr_tunning = mrr_tunning.OrderByDescending (x => x.Item2).ToList ();
			LATENT_FACTORS = mrr_tunning.Select (x => x.Item1).Distinct ().Take (3).ToArray ();
			log.Debug (string.Format ("LATENT FACTORS was changed to: {0}", string.Join (",", LATENT_FACTORS)));
		}

		void TunningLearningRate ()
		{
			var mrr_tunning = new List<Tuple<float, double>> ();

			foreach (var reg in REGULARIZATION) {
				foreach (var num_factors in LATENT_FACTORS) {
					foreach (var learnrate in LEARN_RATE) {
						QueryResult result = Train (num_factors, learnrate, reg);
						double mrr = result.GetMetric ("MRR");
						Log (num_factors, reg, learnrate, mrr);
						mrr_tunning.Add (Tuple.Create (learnrate, mrr));
					}
				}
			}

			mrr_tunning = mrr_tunning.OrderByDescending (x => x.Item2).ToList ();
			LEARN_RATE = mrr_tunning.Select (x => x.Item1).Distinct ().Take (3).ToArray ();
			log.Debug (string.Format ("LEARN RATE was changed to: {0}", string.Join (",", LEARN_RATE)));
		}

		double best_mrr = 0.0;

		QueryResult Train (uint num_factors, float learn_rate, float regularization)
		{
			bool evaluate = true;
			QueryResult result = null;
			while (evaluate) {
				try {
					CreateModel (typeof (MultiCoreBPRMF));
					((MultiCoreBPRMF)Recommender).Feedback = Feedback;
					((MultiCoreBPRMF)Recommender).NumIter = 25;
					((MultiCoreBPRMF)Recommender).NumFactors = num_factors;
					((MultiCoreBPRMF)Recommender).LearnRate = learn_rate;
					((MultiCoreBPRMF)Recommender).RegI = regularization;
					((MultiCoreBPRMF)Recommender).RegU = regularization;
					((MultiCoreBPRMF)Recommender).RegJ = regularization * 0.1f;

					TimeSpan t = Wrap.MeasureTime (delegate () {
						Train ();
						result = Evaluate ();
					});

					Console.WriteLine ("Training and Evaluate model: {0} seconds", t.TotalSeconds);

					double mrr = result.GetMetric ("MRR");
					if (mrr >= best_mrr) {
						best_mrr = mrr;
						string filename = string.Format ("BPRMF-n{0}-l{1}-r{2}", num_factors, learn_rate, regularization);
						SaveModel (string.Format ("output/model/{0}.model", filename));
						MyMediaLite.Helper.Utils.SaveRank (filename, result);
					}

					evaluate = false;
				} catch (Exception ex) {
					Console.WriteLine (ex.Message);
					evaluate = true;
				}
			}

			return result;
		}

		void Log (uint num_factors, float regularization, float learn_rate, double metric)
		{
			log.Info (string.Format ("n={0}\tl={1}\tr={2}\t\t-\t\tMRR = {3}", num_factors,
									 learn_rate, regularization, metric));
		}

		public override void Evaluate (string filename)
		{
			if (!string.IsNullOrEmpty (filename)) {
				Console.WriteLine ("Loading test data");
				if (string.IsNullOrEmpty(path_test) || !path_test.Equals (filename, StringComparison.InvariantCultureIgnoreCase)) {
					Test = LoadTest (filename);
					TestFeedback = LoadPositiveFeedback (filename, ItemDataFileFormat.IGNORE_FIRST_LINE);
				}

				var result = Evaluate ();
				MyMediaLite.Helper.Utils.SaveRank ("bprmf", result);
				Log (((MultiCoreBPRMF)Recommender).NumFactors, ((MultiCoreBPRMF)Recommender).RegI, 
				     ((MultiCoreBPRMF)Recommender).LearnRate, result.GetMetric ("MRR"));
			}
		}

		public override void SetupOptions (string [] args)
		{
			base.SetupOptions (args);

			var options = new OptionSet {
				{ "iter=", v => ((MultiCoreBPRMF)Recommender).NumIter = uint.Parse(v)},
				{ "num-factors=", v => ((MultiCoreBPRMF)Recommender).NumFactors = uint.Parse(v)},
				{ "regularization=", v => ((MultiCoreBPRMF)Recommender).RegU = float.Parse(v)},
				{ "learn-rate=", v => ((MultiCoreBPRMF)Recommender).LearnRate = float.Parse(v)}};

			options.Parse (args);

			((MultiCoreBPRMF)Recommender).RegI = ((MultiCoreBPRMF)Recommender).RegU;
			((MultiCoreBPRMF)Recommender).RegJ = ((MultiCoreBPRMF)Recommender).RegI * 0.1f;
			                             
			log.Info ("Parameters configured!");
			log.Info ("Num Factors: " + ((MultiCoreBPRMF)Recommender).NumFactors);
			log.Info ("Regularization: " + ((MultiCoreBPRMF)Recommender).RegU);
			log.Info ("Learn Rate: " + ((MultiCoreBPRMF)Recommender).LearnRate);


		}
	}
}
