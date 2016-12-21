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
using Baselines.Algorithms;
using System.Linq;
using MyMediaLite;
using Baselines.Service;
using MyMediaLite.Data;

namespace Baselines
{

	class OldProgram
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger (System.Reflection.MethodBase.GetCurrentMethod ().DeclaringType);

		private const string MODEL_FOLDER = "/Volumes/Tyr/Projects/UFMG/Baselines/MyMediaLite-3.11/bin/"; //model-100/
		private const string DATASET_FOLDER = "/Volumes/Tyr/Projects/UFMG/Datasets/Ours/nyc/";
		private static readonly float [] LEARN_RATE = { 0.001f, 0.005f, 0.01f, 0.02f, 0.03f, 0.04f, 0.05f, 0.06f, 0.07f, 0.1f };
		private static readonly uint [] LATENT_FACTORS = { 5, 10, 20, 30, 50, 100, 500, 1000 };
		private static readonly float [] REGULARIZATION = { 0.0025f, 0.01f, 0.03f, 0.04f, 0.05f, 0.06f, 0.07f, 0.1f };
		private static readonly Type BASELINE_TYPE = typeof (RunWeightedBPRMF);
		private string mFold;
		private string mTraining;
		private string mValidation;
		//private string mTest;

		BaselineService mService;

		static void OldMain (string [] args)
		{
			var program = new OldProgram ();
			program.mFold = "fold_1";
			program.mTraining = string.Format ("{0}/{1}/train_all.txt", DATASET_FOLDER, program.mFold);
			program.mValidation = string.Format ("{0}/{1}/validation.txt", DATASET_FOLDER, program.mFold);
			//program.mTest = String.Format ("{0}/{1}/test.txt", DATASET_FOLDER, program.mFold);
			program.Run (args);

#if DEBUG
			Console.WriteLine ("Press enter to close...");
			Console.ReadLine ();
#endif
		}


		void Run (string [] args)
		{
			log.Info ("Running baselines");

			mService = new BaselineService (mTraining, mValidation);

			//TestResults ();
			//TestSameResult ();
			//PerformingRegularization ();
			//PerformingLatentFactors ();
			PerformingLearningRate ();

			log.Info ("Process ending");
		}


		void PerformingRegularization ()
		{
			log.Info ("Tunning REGULARIZATION parameter");
			log.Info ("10 latent features (default), 0.05 learning rate (default)");
			foreach (var reg in REGULARIZATION) {
				IBaseline algorithm = CreateModel (10, 0.05f, reg);
				QueryResult result = EvaluationModel (algorithm);
				log.Info (string.Format ("n={0}\tl={1}\tr={2}\t\t - MRR={3}",
									   10, 0.05f, reg, result.GetMetric ("MRR")));
			}
		}

		void PerformingLatentFactors ()
		{
			//float [] best_regularization = { 0.0025f, 0.1f, 0.2f, 0.07f, 0.06f };
			float [] best_regularization = { 0.07f, 0.06f };

			log.Info ("Tunning LATENT FACTORS parameter");
			foreach (var reg in best_regularization) {
				foreach (var latent_factor in LATENT_FACTORS) {
					IBaseline algorithm = CreateModel (latent_factor, 0.05f, reg);
					QueryResult result = EvaluationModel (algorithm);
					log.Info (string.Format ("n={0}\tl={1}\tr={2}\t\t - MRR={3}",
											 latent_factor, 0.05f, reg, result.GetMetric ("MRR")));
				}
			}
		}

		void PerformingLearningRate ()
		{
			//uint [] best_latent_factors = { 10, 50, 100, 500, 1000 };
			//float [] best_regularization = { 0.1f, 0.2f, 0.07f, 0.06f };

			uint [] best_latent_factors = { 10, 50, 100, 500, 1000 };
			float [] best_regularization = { 0.07f, 0.06f };

			log.Info ("Tunning LEARNING RATE parameter");
			foreach (var reg in best_regularization) {
				foreach (var latent_factor in best_latent_factors) {
					foreach (var learning_rate in LEARN_RATE) {
						Evaluate (latent_factor, learning_rate, reg);
					}
				}
			}
		}

		void Evaluate (uint latent_factor, float learning_rate, float reg)
		{
			bool running = true;
			int attempt = 1;
			while (running && attempt <= 3) {
				try {
					attempt++;
					IBaseline algorithm = CreateModel (latent_factor, learning_rate, reg);
					QueryResult result = EvaluationModel (algorithm);
					log.Info (string.Format ("n={0}\tl={1}\tr={2}\t\t - MRR={3}",
											 latent_factor, learning_rate, reg, result.GetMetric ("MRR")));
					running = false;
				} catch (Exception ex) {
					Console.WriteLine (ex.Message);
					running = true;

					if (attempt > 3)
						log.Error (string.Format ("n={0}\tl={1}\tr={2}\t\t\t ERROR!!!", latent_factor, learning_rate, reg));
				}
			}
		}

		IBaseline CreateModel (uint num_factors, float learn_rate, float regularization)
		{
			MyMediaLite.Random.Seed = 23;
			IBaseline algorithm = mService.CreateModel (BASELINE_TYPE);
			algorithm.SetParameter ("NumFactors", num_factors);
			algorithm.SetParameter ("NumIter", 25);
			algorithm.SetParameter ("RegU", regularization);
			algorithm.SetParameter ("RegI", regularization);
			algorithm.SetParameter ("RegJ", regularization * 0.1f);
			algorithm.SetParameter ("LearnRate", learn_rate);

			TimeSpan t = Wrap.MeasureTime (delegate () {
				mService.TrainModel (algorithm, MyMediaLite.IO.ItemDataFileFormat.IGNORE_FIRST_LINE);
			});

			Console.WriteLine ("Training model {0}: {1} seconds", algorithm.Name (), t.TotalSeconds);
			Console.WriteLine ("Algorithm parameters: {0}\n\n", algorithm);

			return algorithm;
		}

		//IBaseline CreateModel (List<KeyValuePair<string, object>> parameters)
		//{
		//	MyMediaLite.Random.Seed = 23;
		//	IBaseline algorithm = mService.CreateModel (BASELINE_TYPE);

		//	foreach (var item in parameters)
		//		algorithm.SetParameter (item.Key, item.Value);

		//	TimeSpan t = Wrap.MeasureTime (delegate () {
		//		mService.TrainModel (algorithm, MyMediaLite.IO.ItemDataFileFormat.IGNORE_FIRST_LINE);
		//	});

		//	Console.WriteLine ("Training model {0}: {1} seconds", algorithm.Name (), t.TotalSeconds);
		//	Console.WriteLine ("Algorithm parameters: {0}\n\n", algorithm);

		//	return algorithm;
		//}

		IBaseline LoadModel ()
		{
			string model = string.Format ("{0}/model-n100.test", MODEL_FOLDER);
			return mService.LoadModel (BASELINE_TYPE, model);
		}

		QueryResult EvaluationModel (IBaseline algorithm)
		{
			QueryResult result = null;
			TimeSpan t = Wrap.MeasureTime (delegate () { result = mService.EvaluationRank (algorithm); });

			Console.WriteLine ("Predicting {0} items: {1} seconds", result.Items.Count (), t.TotalSeconds);

			return result;
		}

	}
}
