// Copyright (C) 2016 Jordan Silva
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
using System.Collections.Generic;
using MyMediaLite.Data;
using MyMediaLite.Helper;
using System.IO;
using MyMediaLite.IO;
using System.Threading.Tasks;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Data.Text;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;
using System.Runtime;

namespace MyMediaLite.RatingPrediction
{
	/// <summary>Geographical factorization method proposed by Li et al.</summary>
	/// <remarks>
	///   <para>
	///     We use the fast learning method proposed by Hu et al. (alternating least squares, ALS),
	///     and we use a global parameter to give observed values higher weights.
	///   </para>
	///   <para>
	///     Literature:
	///     <list type="bullet">
	///       <item><description>
	///         Xutao Li, Gao Cong, Xiao-Li Li, Tuan-Anh Nguyen Pham, and Shonali Krishnaswamy. 2015. 
	///         Rank-GeoFM: A Ranking based Geographical Factorization Method for Point of Interest Recommendation. 
	///         ACM SIGIR 2015. 
	///         http://dx.doi.org/10.1145/2766462.2767722
	///       </description></item>
	///     </list>
	///   </para>
	///   <para>
	///     This recommender supports incremental updates.
	///   </para>
	/// </remarks>
	public class RankGeoFM_modified : TimeAwareRatingPredictor
	{

		#region Fields
		ParallelOptions opts = new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32 (Math.Ceiling ((Environment.ProcessorCount * 0.95) * 1.0)) };

		const string FILENAME_DISTANCE_MATRIX = @"DistanceMatrix.bin.RankGeoFM";
		const string FILENAME_DISTANCE_MATRIX_IDX = @"DistanceMatrixIndex.bin.RankGeoFM";
		const string FILENAME_U1 = @"U1.bin.RankGeoFM";
		const string FILENAME_U2 = @"U2.bin.RankGeoFM";
		const string FILENAME_L1 = @"L1.bin.RankGeoFM";
		const string FILENAME_W = @"W.bin.RankGeoFM";
		const string FILENAME_MAP_USERS = @"MapUsers.bin.RankGeoFM";
		const string FILENAME_MAP_ITEMS = @"MapItems.bin.RankGeoFM";
		const string FILENAME_UIF = @"UIF.bin.RankGeoFM";
		const string FILENAME_FG = @"FG.bin.RankGeoFM";
		const string FILENAME_UL = @"UL.bin.RankGeoFM";
		const string FILENAME_UFG = @"UFG.bin.RankGeoFM";


		Matrix<double> distanceMatrix;
		DataType.Matrix<int> distanceMatrixIndex;
		Matrix<double> W;
		Matrix<double> UIF;

		Matrix<double> UL;
		Matrix<double> FG;
		Matrix<double> UFG;

		double [] lossWeight;
		int totalUsers;
		int totalItems;
		int totalItemsReal;

		/// <summary>
		/// "Model Paramaeter U^(1) used to model the user's own preference. U(1) ∈ R|U|×K" [1]
		/// </summary>
		private Matrix<double> U_1;

		/// <summary>
		/// On the other hand, we introduce one extra latent factor matrix U(2) ∈ R|U|×K for users, 
		/// and employ U(2) to model the interaction between users and POIs for incorporating the geographical influence.
		/// </summary>
		private Matrix<double> U_2;

		/// <summary>
		/// "Model Paramaeter L^(1) used to model the user's own preference. L(1) ∈ R|L|×K" [1]
		/// </summary>
		private Matrix<double> L_1;

		#endregion

		#region Properties

		/// <summary>
		/// Hyperparameter k_1 is the number of neighbors considered for Geographical influence
		/// </summary>
		public uint K_1 { get { return k_1; } set { k_1 = value; } }
		uint k_1 = 300;

		/// <summary>K is the number of dimensions of latent space Θ</summary> 
		public uint K { get; set; }

		/// <summary>
		/// Hyperparameter ε initialized as in [1]
		/// The Epsilon is named as maginal in the original Matlab code.
		/// </summary>
		public float ε { get; set; }

		/// <summary>
		/// Hyperparameter C initialized as in [1] eq(5), eq(6) and eq(7)
		/// The C is named as C in original Matlab code.
		/// </summary>
		public float C { get; set; }

		/// <summary>
		/// Hyperparameter Learning rate γ initialized as in [1]
		/// The Gamma is named as alpha in the original Matlab code.
		/// </summary>
		public float γ { get; set; }

		/// <summary>
		/// Hyperparameter Alpha initialize as in [1] eq(7)
		/// "As a result, tuning the hyperparameter α can balance the contributions of user-preference and geographical influence scores to the final recommendation score." [1]
		/// "We find that Rank-GeoFM perfoms the best at α = 0.2 for POI recommendation on both data, and performs the best at α=0.1 for time-aware POI recommendation on both data. " [1]
		/// The Alpha is named as FactorInf in the original Matlab code.
		/// </summary>
		public float α { get; set; }

		/// <summary>Number of maximum iterations</summary>
		public uint MaxIterations { get; set; }

		/// <summary>The POIs items.</summary>
		public IList<POI> Items { get; set; }

		/// <summary>Feedback data </summary>
		public IPosOnlyFeedback Feedback { get; set; }

		/// <summary>Validation data </summary>
		public IPosOnlyFeedback Validation { get; set; }

		/// <summary>Item Mapping</summary>
		public IMapping ItemMapping { get; set; }

		/// <summary>User Mapping</summary>
		public IMapping UserMapping { get; set; }

		#endregion

		///
		public RankGeoFM_modified ()
		{
			K = 100;
			C = 1.0f;
			ε = 0.3f;
			γ = 0.0001f;
			α = 0.2f;
			MaxIterations = 1000;

			//Control.NativeProviderPath = @"/Volumes/Tyr/Projects/UFMG/Baselines/Jordan/MyMediaLite-Research/src/Programs/Baselines/bin/Debug/";
			//FIXME: Return UseNativeMKL
			Control.UseNativeMKL ();
			System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
			GC.Collect ();
			//Control.LinearAlgebraProvider = new MklLinearAlgebraProvider ();
		}

		/// <summary>
		/// Initialize configurations and data for training.
		/// </summary>
		void Initialize ()
		{
			totalUsers = UserMapping.NumberOfEntities;
			totalItems = ItemMapping.NumberOfEntities;
			totalItemsReal = Items.Select (x => x.Id).ToList ().Max ();

			Console.WriteLine ("User Max Id: {0}, Item Max Id: {1}", totalUsers, totalItems);
			Console.WriteLine (Feedback.Statistics (Validation));

			ComputeDistanceMatrix ();
			CreateWeightedMatrix ();

			CreatePreferenceMatrix ();
			InitializeLossWeight ();
			CreateUserItemFrequencyMatrix ();
		}

		#region Distance Matrix

		/// <summary>
		/// Initialize the distance matrix between all items.
		/// Store only K nearest neighbors of each item.
		/// </summary>
		void ComputeDistanceMatrix ()
		{
			if (Items == null)
				throw new Exception ("Items can not be null");

			if (File.Exists (FILENAME_DISTANCE_MATRIX)) {
				Console.WriteLine ("{0} Loading distance matrix", DateTime.Now);

				if (!File.Exists (FILENAME_W))
					distanceMatrix = MatrixMarketReader.ReadMatrix<double> (FILENAME_DISTANCE_MATRIX, Compression.GZip);

				distanceMatrixIndex = (DataType.Matrix<int>)FileSerializer.Deserialize (FILENAME_DISTANCE_MATRIX_IDX);
			} else {
				Console.WriteLine ("{0} Computing distance matrix", DateTime.Now);

				distanceMatrix = CreateMatrix.Dense<double> (totalItemsReal + 1, (int)k_1);
				distanceMatrixIndex = new DataType.Matrix<int> (totalItemsReal + 1, k_1);

				var i = 0;
				Parallel.ForEach (Items, opts, item => CalculateDistancesItem (item, ref i));
				SaveDistanceMatrix ();
			}
		}

		/// <summary>
		/// Calculate POI distance between all POIs
		/// Store only K nearest neighbors of each item.
		/// </summary>
		/// <param name="item">POI</param>
		/// <param name="count">Count for print status of progress</param>
		void CalculateDistancesItem (POI item, ref int count)
		{
			try {
				var dist = new double [totalItemsReal + 1];
				dist [0] = double.MaxValue;
				foreach (var item2 in Items) {
					if (item.Id == item2.Id)
						dist [item2.Id] = double.MaxValue;
					else
						dist [item2.Id] = DistanceHelper.Distance (item.Coordinates.Latitude, item.Coordinates.Longitude,
																	item2.Coordinates.Latitude, item2.Coordinates.Longitude);
				}

				var sorted = dist.Select ((x, i) => new KeyValuePair<double, int> (x, i))
								 .OrderBy (x => x.Key)
								 .Take ((int)k_1).ToList ();
				distanceMatrix.SetRow (item.Id, sorted.Select (x => x.Key).ToArray ());
				distanceMatrixIndex.SetRow (item.Id, sorted.Select (x => x.Value).ToArray ());
				dist = null;

				count++;
				if ((count % 10000) == 0) {
					Console.WriteLine ("{0} - {1}", DateTime.Now, count);
				}
			} catch (Exception ex) {
				throw ex;
			}
		}

		/// <summary>
		/// Creates the W matrix that contains probabilities that user visits POI l when l' has been visited.
		/// </summary>
		void CreateWeightedMatrix ()
		{
			if (File.Exists (FILENAME_W)) {
				Console.WriteLine ("{0} Loading weighted matrix", DateTime.Now);

				W = MatrixMarketReader.ReadMatrix<double> (FILENAME_W, Compression.GZip);
			} else {
				Console.WriteLine ("{0} Creating weighted matrix", DateTime.Now);

				W = CreateMatrix.Dense<double> (totalItemsReal + 1, (int)k_1);

				var count = 1;
				Parallel.For (0, distanceMatrix.RowCount, opts, index => ComputeItemWeightedMatrix (index, ref count));

				MatrixMarketWriter.WriteMatrix (FILENAME_W, W, Compression.GZip);
			}
		}

		/// <summary>
		/// Computes the weighted matrix.
		/// </summary>
		/// <param name="i">The index.</param>
		/// <param name="count">Count.</param>
		void ComputeItemWeightedMatrix (int i, ref int count)
		{
			//for (int i = 0; i < distanceMatrix.NumberOfRows; i++) {
			if (distanceMatrix.Row (i).Count < K)
				throw new Exception (string.Format ("Number of nearest neighbors ({0}) is less than K={1}",
												   distanceMatrix.Row (i).Count, K));

			var values = distanceMatrix.Row (i);
			var reg_values = values.Select (x => (x < 0.5 && x > 0) ? 0.5 : x * 1.0).ToArray ();
			var total = reg_values.Select (x => (1.0 / x)).Sum ();
			reg_values = reg_values.Select (x => ((1.0 / x) / total)).ToArray ();
			W.SetRow (i, reg_values);

			count++;
			if ((count % 10000) == 0)
				Console.WriteLine ("{0} - {1}", DateTime.Now, count);
			//}
		}

		#endregion

		#region Latent Factors Matrices

		void CreatePreferenceMatrix ()
		{
			if (File.Exists (FILENAME_U1)) {
				Console.WriteLine ("{0} Loading latent factors matrix", DateTime.Now);

				U_1 = MatrixMarketReader.ReadMatrix<double> (FILENAME_U1, Compression.GZip);
				U_2 = MatrixMarketReader.ReadMatrix<double> (FILENAME_U2, Compression.GZip);
				L_1 = MatrixMarketReader.ReadMatrix<double> (FILENAME_L1, Compression.GZip);
			} else {
				Console.WriteLine ("{0} Creating latent factors matrix", DateTime.Now);

				var normalDist = new Normal (0.0, 0.01);
				normalDist.RandomSource = new System.Random (34);

				U_1 = CreateMatrix.Random<double> (totalUsers, (int)K, normalDist);
				U_2 = CreateMatrix.Random<double> (totalUsers, (int)K, normalDist);
				L_1 = CreateMatrix.Random<double> (totalItemsReal, (int)K, normalDist);
			}
		}

		#endregion

		#region Creating Temporaty Matrices

		void CreateUserItemFrequencyMatrix ()
		{
			if (File.Exists (FILENAME_UIF)) {
				Console.WriteLine ("{0} Loading user-item frequency matrix", DateTime.Now);

				UIF = MatrixMarketReader.ReadMatrix<double> (FILENAME_UIF, Compression.GZip);
			} else {
				Console.WriteLine ("{0} Creating user-item frequency matrix", DateTime.Now);

				UIF = CreateMatrix.Dense<double> (totalUsers, totalItems);
				Parallel.For (0, ratings.Count, opts, item => CreateUserItemMatrix (item));

				MatrixMarketWriter.WriteMatrix (FILENAME_UIF, UIF, Compression.GZip);
			}
		}

		void CreateUserItemMatrix (int index)
		{
			try {
				int user = ratings.Users [index];
				int item = ratings.Items [index];
				UIF [user, item] += 1.0;
			} catch (Exception ex) {
				Console.WriteLine (ex.Message);
				Console.WriteLine ("i: {0}", index);
				Console.WriteLine ("user: {0}, item: {1}", ratings.Users [index], ratings.Items [index]);
				throw ex;
			}
		}

		void CalculatingTemporaryMatrices ()
		{
			Console.WriteLine ("{0} Normalizing Geographic Matrix", DateTime.Now);
			var factor = C * α;
			var count = U_2.RowCount;
			Parallel.For (0, count, opts, index => {
				var norm = U_2.Row (index).L2Norm ();
				if (norm > factor) {
					U_2.SetRow (index, (factor * U_2.Row (index) / norm));
				}
			});

			Console.WriteLine ("{0} Creating FG matrix", DateTime.Now);
			FG = CreateMatrix.Dense<double> (totalItemsReal, (int)K);
			//for (int i = 0; i < totalItems; i++) {
			//  CreateFGMatrix (i);
			//}

			Parallel.For (1, totalItemsReal, opts, index => CreateFGMatrix (index));

			//Console.WriteLine ("{0} Creating UL matrix", DateTime.Now);
			UL = U_1 * L_1.Transpose();

			//Console.WriteLine ("{0} Creating UFG matrix", DateTime.Now);
			UFG = U_2 * FG.Transpose ();

			//Parallel.For (0, 2, opts, i => {
			//  if (i == 0)
			//      UL = U_1 * L_1.Transpose ();
			//  if (i == 1)
			//      UFG = U_2 * FG.Transpose ();
			//});

			//Console.WriteLine ("{0} matrix completed", DateTime.Now);
		}


		void CreateFGMatrix (int location)
		{
			for (int i = 0; i < k_1; i++) {
				try {
					FG.SetRow (location, FG.Row (location) + W [location, i] * L_1.Row (distanceMatrixIndex [location, i] - 1));
					//FG.SetRow (location - 1, FG.Row (location - 1) + W [location, i] * L_1.Row (distanceMatrixIndex [location, i] - 1));
				} catch (Exception ex) {
					Console.WriteLine ("location: {0}, i: {1}", location, i);
					Console.WriteLine ("W [location, i]: {0}", W [location, i]);
					Console.WriteLine ("distanceMatrixIndex [location, i]: {0}", distanceMatrixIndex [location, i]);
					Console.WriteLine ("L_1 Row: {0}", L_1.Row (distanceMatrixIndex [location, i] - 1));
					throw ex;
				}
			}
		}

		//void CreateULUFG (int user)
		//{
		//  for (int item = 1; item <= totalItems; item++) {
		//      UL [user, item] = U_1.Row (user) * L_1.Row (item);
		//      UFG [user, item] = U_2.Row (user) * FG.Row (item);
		//  }
		//}

		#endregion


		void InitMatrixNormal (IList<int> ids, ref Matrix<double> M, ref Dictionary<int, int> mapper, uint k)
		{
			var normalDist = new Normal (0.0, 0.01);
			normalDist.RandomSource = new System.Random (34);
			int i = 0;
			foreach (int id in ids) {
				for (int j = 0; j < k; j++) {
					M [i, j] = normalDist.Sample ();
					mapper [id] = i;
				}
				i++;
			}
		}

		/// <summary>
		/// Gets the nearest neighbors of the POI.
		/// </summary>
		/// <param name="id">Id</param>
		/// <returns>The nearest neighbors.</returns>
		public IList<double> GetNearestNeighborsItem (int id)
		{
			return distanceMatrix.Row (id - 1);
		}

		#region Predict Functions

		///
		public override float Predict (int user_id, int item_id)
		{
			//user_id = UserMapping.ToInternalID (user_id.ToString ());
			//item_id = ItemMapping.ToInternalID (item_id.ToString ());

			var result = ComputeRecommendationScore (user_id, item_id);
			float floatResult = (float)result;
			if (float.IsPositiveInfinity (floatResult)) {
				floatResult = float.MaxValue;
			} else if (float.IsNegativeInfinity (floatResult)) {
				floatResult = float.MinValue;
			}

			return floatResult;
		}

		///
		public override float Predict (int user_id, int item_id, DateTime time)
		{
			throw new NotImplementedException ();
		}

		/// <summary>
		/// Computes the recommendation score Eq (4)
		/// </summary>
		/// <returns>The recommendation score.</returns>
		/// <param name="user">user</param>
		/// <param name="item">location</param>
		private double ComputeRecommendationScore (int user, int item)
		{

			//Compute with temporaries matrices
			var user_pref = UL [user, item];
			var geo_pref = UFG [user, item];

			//TODO: Compute with no temporary matrices
			//var user_pref = ComputeUserPreferenceScore (user, item);
			//var geo_pref = ComputeGeographicalInfluenceScore (user, item);

			return user_pref + geo_pref;
		}

		//UL
		double ComputeUserPreferenceScore (int user, int location)
		{
			location = int.Parse (ItemMapping.ToOriginalID (location)) - 1;
			var score = U_1.Row (user) * L_1.Transpose ().Column (location);

			//var score = DataType.VectorExtensions.ScalarProduct (u_row, l_row);
			return score;
		}

		//UFG
		double ComputeGeographicalInfluenceScore (int user, int location)
		{
			location = int.Parse (ItemMapping.ToOriginalID (location)) - 1;
			var score = U_2.Row (user) * FG.Transpose ().Column (location);
			//var score = DataType.VectorExtensions.ScalarProduct (u_row, sum);
			return score;
		}


		/// <summary>
		/// Computes the geographical weight sum.
		/// Sum the k neighbors weight of the item × latent factors of each item
		/// This methods represents F_G in the original MatLab code.
		/// </summary>
		/// <param name="location">POI</param>
		/// <returns>The geographical weight sum.</returns>
		Vector<double> ComputeGeographicalWeightSum (int location)
		{
		  var sum = CreateVector.Dense<double> ((int)K);

		  for (int i = 0; i < k_1; i++) {
		      //w_ℓℓ
		      var weight = W [location, i];

		      //l_ℓ*
		      var uid = distanceMatrixIndex [location, i];
		      var l_features = L_1.Row (uid) * weight;
		      sum += l_features;
		  }

		  return sum;
		}

		#endregion

		///
		public override void Train ()
		{
			Initialize ();

			Console.WriteLine ("{0} Training data...", DateTime.Now);
			//double threshold = C * Alpha;
			//double best_precision = 0.0;

			var index = ratings.RandomIndex;
			var best_value = 0.0;
			object [] best_matrix = null;

			var evaluations = new List<string> ();
			for (int iter = 1; iter <= MaxIterations; iter++) {
				TimeSpan t = Wrap.MeasureTime (delegate () {
					CalculatingTemporaryMatrices ();
					Console.WriteLine ("Temporary matrices built!");

					if (Validation != null) {
					  var results = Eval.Items.Evaluate (this, Validation, Feedback, n: 10);
					  string text = string.Format ("iteration={0}, @10, pre={1}, recall={2}", iter, results ["prec@10"], results ["recall@10"]);
					  evaluations.Add (text);
					  Console.WriteLine (text);
					  if (results ["prec@10"] > best_value) {
					      best_value = results ["prec@10"];
					      best_matrix = new [] { U_1, L_1, U_2, FG, UFG, UL };
					  }
					}

					//Matrices to compare difference after each iteration
					Console.WriteLine ("Instancing matrices to compare difference with olders matrices");
					var U_1_pre = Matrix<double>.Build.DenseOfMatrix (U_1);
					var U_2_pre = Matrix<double>.Build.DenseOfMatrix (U_2);
					var L_1_pre = Matrix<double>.Build.DenseOfMatrix (L_1);

					if (iter == 1) {
						//GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
						//GC.Collect ();
						//Console.WriteLine ("Computing difference with older matrices!");
						//var u1_diff = U_1_pre - U_1;
						//var u1_diff_norm = u1_diff.L2Norm ();
						//u1_diff = null;

						//Console.WriteLine ("First diff");
						//var u2_diff = U_2_pre - U_2;
						//var u2_diff_norm = u2_diff.L2Norm ();
						//u2_diff = null;
						//Console.WriteLine ("Second diff");

						//var l1_diff = (L_1_pre - L_1);
						//var l1_diff_norm = l1_diff.L2Norm (); //DataType.MatrixExtensions.EuclideanNorm (l1_diff);//
						//l1_diff = null;
						//Console.WriteLine ("Third diff");
						//Console.WriteLine ("Iteration 0 - Latent Diffs: {0}", u1_diff_norm + u2_diff_norm + l1_diff_norm);
						var first_diff = (U_1_pre - U_1).L2Norm () + (U_2_pre - U_2).L2Norm () + (L_1_pre - L_1).L2Norm ();
						Console.WriteLine ("Iteration 0 - Latent Diffs: {0}", first_diff);
					}

					Console.WriteLine ("Starting iteration");
					Iterate (index);
					var diff = (U_1_pre - U_1).L2Norm () + (U_2_pre - U_2).L2Norm () + (L_1_pre - L_1).L2Norm ();
					Console.WriteLine ("Iteration {0} - Latent Diffs: {1}", iter, diff);
				});

				Console.WriteLine ("Iteration {0}: {1} seconds", iter, t.TotalSeconds);
			}

			//Saving Weighted Matrix
			SaveModel (null);

			StreamWriter file = File.CreateText ("iterations.txt");
			file.WriteLine (string.Join ("\n", evaluations));
			file.Close ();

			if (best_matrix != null) {
				U_1 = (Matrix<double>)best_matrix [0];
				L_1 = (Matrix<double>)best_matrix [1];
				U_2 = (Matrix<double>)best_matrix [2];
				FG = (Matrix<double>)best_matrix [3];
				UFG = (Matrix<double>)best_matrix [4];
				UL = (Matrix<double>)best_matrix [5];
				SaveModel ("best_iteration");
			}
		}

		void Iterate (IList<int> rating_indices)
		{
			//var locations = new int [totalItems];
			var random = new System.Random ();
			//RandomExtensions.NextInt32s (random, locations, 1, totalItems);

			int count = 1;

			//Iteration through all checkins
			foreach (var index in rating_indices) {
				try {
					int user = ratings.Users [index];
					int item1 = ratings.Items [index];

					var x_score = ComputeRecommendationScore (user, item1);
					var x_freq = UIF [user, item1];

					////Sampling ranking (lines 5~8)
					var item2 = -1;
					var n = 0;
					var y_score = 0.0;
					var y_freq = 0.0;

					while (true) {
						var item = random.Next (0, totalItems);

						y_score = ComputeRecommendationScore (user, item);
						y_freq = UIF [user, item];
						n++;

						if (Incompatibility (x_score, x_freq, y_score, y_freq) == 1) {
							item2 = item;
							break;
						}

						if (n >= totalItems)
							break;
					}

					//Updating relevant latent factors by using SGD method (lines 9~15)
					if (item2 != -1) {
						//ƞ
						n = Convert.ToInt32 (Math.Floor ((totalItems - 1.0) / (n * 1.0)));
						//matlab code
						//double ƞ = E (n);

						//paper implementation
						double ƞ = E (n) * deltaFunction (x_score, y_score);

						UpdateRelevantFactors (item1, item2, user, ƞ);
					}

					count++;
					if (count % 10000 == 0)
						Console.Write (".");
				} catch (Exception ex) {
					Console.WriteLine (ex.Message);
					Console.WriteLine ("Rating index: {0}", index);
				}
			}

			Console.WriteLine ();
		}

		/// <summary>
		/// Update relevant latent factors
		/// </summary>
		/// <param name="item1">POI ℓ</param>
		/// <param name="item2">POI ℓ'</param>
		/// <param name="user">User id</param>
		/// <param name="ƞ">loss ƞ</param>
		void UpdateRelevantFactors (int item1, int item2, int user, double ƞ)
		{
			//g
			//paper
			var g = FG.Row (item2) - FG.Row (item1);
			//matlab implementation
			//var g = FG.Row (item1) - FG.Row (item2);

			var l1_item1 = L_1.Row (item1);
			var l1_item2 = L_1.Row (item2);

			//updating U_1 (paper implementation)
			//eq. U_1 = u1 - γƞ(L_l' - L_l)
			var u1_new = U_1.Row (user) - (γ * ƞ * (l1_item2 - l1_item1));

			//matlab code
			//eq. U_1 = u1 + γƞ(L_l - L_l')
			//var u1_new = U_1.Row (user) + (γ * ƞ * (l1_item1 - l1_item2));
			U_1.SetRow (user, u1_new);

			//updating U_2 (paper implementation)
			//eq. U_2 = u2 - γƞg
			var u2_new = U_2.Row (user) - (γ * ƞ * g);

			//matlab code
			//eq. U_2 = u2 + γƞg
			//var u2_new = U_2.Row (user) + (γ * ƞ * g);
			U_2.SetRow (user, u2_new);

			//γƞU_1
			var wu1 = γ * ƞ * U_1.Row (user);

			//updating L_l (item 1)
			//eq. L_1 = L_l + γƞU_1
			L_1.SetRow (item1, l1_item1 + wu1);

			//updating L_l' (item 2)
			//eq. L_1 = L_l' - γƞU_1
			//l1_item2 = l1_item2 + (γ * (-ƞ * U_1.Row (user)));
			L_1.SetRow (item2, l1_item2 - wu1);


			//Project the update lataent factors to enforce constraints in Eqs. (5) ~ (7),
			UpdateRelevantFactorsConstraints (U_1, user, C);
			UpdateRelevantFactorsConstraints (U_2, user, C * α);
			UpdateRelevantFactorsConstraints (L_1, item1, C);
			UpdateRelevantFactorsConstraints (L_1, item2, C);
		}

		void UpdateRelevantFactorsConstraints (Matrix<double> matrix, int rowIndex, float normalizeValue)
		{
			var row = matrix.Row (rowIndex);

			var norm = row.L2Norm ();
			if (norm > normalizeValue)
				matrix.SetRow (rowIndex, C * row / norm);
		}

		void InitializeLossWeight ()
		{
			Console.WriteLine ("{0} Initializing loss weight function", DateTime.Now);

			lossWeight = new double [totalItems];
			double lossWeightTotal = 0.0;

			for (int i = 0; i < totalItems; i++) {
				var loss = lossWeightTotal + 1.0 / (i + 1);
				lossWeight [i] = loss;
				lossWeightTotal = loss;
			}
		}

		/// <summary>
		/// Indicator the specified statement.
		/// </summary>
		/// <param name="statement">If set to <c>true</c> statement.</param>
		int I (bool statement)
		{
			return (statement) ? 1 : 0;
		}

		/// <summary>
		/// Ranking Incompatibility function eq(1)
		/// Measures the number of POIs that are incorrectly ranked higher than l for user u.
		/// </summary>
		/// <param name="x_score">Recommendation score of POI ℓ</param>
		/// <param name="x_freq">Users frequency of visits to POI ℓ</param>
		/// <param name="y_score">Recommendation score of POI ℓ'</param>
		/// <param name="y_freq">Users frequency of visits to POI ℓ'</param>
		int Incompatibility (double x_score, double x_freq, double y_score, double y_freq)
		{
			return I (x_freq > y_freq) * I (x_score < (y_score + ε));
		}

		/// <summary>
		/// Computs the ranking incompatibility from incompFunction into a loss eq(2)
		/// </summary>
		/// <param name="r">Rating incompatibility.</param>
		private double E (int r)
		{
			return lossWeight [r];

			//double sum = 0;
			//for (int i = 1; i <= r; i++) {
			//  sum += 1 / i;
			//}
			//return sum;
		}

		/// <summary>
		/// Used to approximate the indicator funciton. [1]
		/// </summary>
		/// <returns>The function.</returns>
		/// <param name="a">The alpha component.</param>
		private double sigmoidFunction (double a)
		{
			return (1 / (1 + Math.Exp (-a)));
		}

		/// <summary>
		/// Function for computing δuℓℓ′ [1].
		/// </summary>
		/// <returns>The function.</returns>
		/// <param name="x_score">Recommendation score of POI ℓ</param>
		/// <param name="y_score">Recommendation score of POI ℓ'</param>
		private double deltaFunction (double x_score, double y_score)
		{
			return (sigmoidFunction (y_score + ε - x_score) * (1 - (sigmoidFunction (y_score + ε - x_score))));
		}

		///
		public override void LoadModel (string file)
		{
			if (!String.IsNullOrEmpty (file))
				file += "/";

			U_1 = MatrixMarketReader.ReadMatrix<double> (file + FILENAME_U1, Compression.GZip);
			U_2 = MatrixMarketReader.ReadMatrix<double> (file + FILENAME_U2, Compression.GZip);
			L_1 = MatrixMarketReader.ReadMatrix<double> (file + FILENAME_L1, Compression.GZip);

			FG = MatrixMarketReader.ReadMatrix<double> (file + FILENAME_FG, Compression.GZip);
			UL = MatrixMarketReader.ReadMatrix<double> (file + FILENAME_UL, Compression.GZip);
			UFG = MatrixMarketReader.ReadMatrix<double> (file + FILENAME_UFG, Compression.GZip);
		}

		///
		public override void SaveModel (string path)
		{
			if (!string.IsNullOrEmpty (path)) {
				if (!Directory.Exists (path))
					Directory.CreateDirectory (path);

				path += "/";
			}

			MatrixMarketWriter.WriteMatrix (path + FILENAME_U1, U_1, Compression.GZip);
			MatrixMarketWriter.WriteMatrix (path + FILENAME_U2, U_2, Compression.GZip);
			MatrixMarketWriter.WriteMatrix (path + FILENAME_L1, L_1, Compression.GZip);

			MatrixMarketWriter.WriteMatrix (path + FILENAME_FG, FG, Compression.GZip);
			MatrixMarketWriter.WriteMatrix (path + FILENAME_UL, UL, Compression.GZip);
			MatrixMarketWriter.WriteMatrix (path + FILENAME_UFG, UFG, Compression.GZip);
		}

		void SaveDistanceMatrix ()
		{
			MatrixMarketWriter.WriteMatrix (FILENAME_DISTANCE_MATRIX, distanceMatrix, Compression.GZip);
			distanceMatrixIndex.Serialize (FILENAME_DISTANCE_MATRIX_IDX);
		}


	}
}
