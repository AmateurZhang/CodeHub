using System;
using System.Threading;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Messaging;
using CodeHub.Helpers;
using CodeHub.Services;
using CodeHub.Views;
using Octokit;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media;
using MarkdownSharp;
using Windows.UI.Core;

namespace CodeHub.ViewModels
{
    public class RepoDetailViewmodel : AppViewmodel
    {
        public Repository _repository;
        public Repository Repository
        {
            get
            {
                return _repository;
              }
            set
            {
                Set(() => Repository, ref _repository, value);

            }
        }

        public bool _isStar;
        public bool IsStar
        {
            get
            {
                return _isStar;
            }
            set
            {
                Set(() => IsStar, ref _isStar, value);
            }
        }

        public bool _IsWatching;
        public bool IsWatching
        {
            get
            {
                return _IsWatching;
            }
            set
            {
                Set(() => IsWatching, ref _IsWatching, value);
            }
        }

        public bool _IsStarLoading;
        public bool IsStarLoading
        {
            get
            {
                return _IsStarLoading;
            }
            set
            {
                Set(() => IsStarLoading, ref _IsStarLoading, value);
            }
        }

        public bool _IsWatchLoading;
        public bool IsWatchLoading
        {
            get
            {
                return _IsWatchLoading;
            }
            set
            {
                Set(() => IsWatchLoading, ref _IsWatchLoading, value);
            }
        }

        public bool _IsForkLoading;
        public bool IsForkLoading
        {
            get
            {
                return _IsForkLoading;
            }
            set
            {
                Set(() => IsForkLoading, ref _IsForkLoading, value);
            }
        }

        public async Task Load(object repo)
        {
            if (!GlobalHelper.IsInternet())
            {
                //Sending NoInternet message to all viewModels
                Messenger.Default.Send(new GlobalHelper.NoInternetMessageType());
            }
            else
            {
                //Sending Internet available message to all viewModels
                Messenger.Default.Send(new GlobalHelper.HasInternetMessageType());

                isLoading = true;

                if (repo.GetType() == typeof(string))
                {
                    //Splitting repository name and owner name
                    var names = (repo as string).Split('/');
                    Repository = await RepositoryUtility.GetRepository(names[0], names[1]);
                }
                else
                {
                    Repository = repo as Repository;
                }

                if (Repository?.Owner != null)
                {

                    // Get the image buffer manually to avoid making the HTTP call twice		 
                    CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    IBuffer buffer = await HTTPHelper.GetBufferFromUrlAsync(Repository.Owner.AvatarUrl, cts.Token);
                    if (buffer != null)
                    {

                        // Load the user image		
                        Tuple<ImageSource, ImageSource> images = await ImageHelper.GetImageAndBlurredCopyFromPixelDataAsync(buffer, 256);
                        UserAvatar = images?.Item1;
                        UserBlurredAvatar = images?.Item2;

                        // Calculate the brightness		
                        byte brightness = await ImageHelper.CalculateAverageBrightnessAsync(buffer);
                        Messenger.Default.Send(new GlobalHelper.SetBlurredAvatarUIBrightnessMessageType { Brightness = brightness });
                    }
                }

                IsStar = await RepositoryUtility.CheckStarred(Repository);
                IsWatching = await RepositoryUtility.CheckWatched(Repository);

                isLoading = false;
            }
        }

        private RelayCommand _sourceCodeNavigate;
        public RelayCommand SourceCodeNavigate
        {
            get
            {
                return _sourceCodeNavigate
                    ?? (_sourceCodeNavigate = new RelayCommand(
                                          () =>
                                          {
                                              SimpleIoc.Default.GetInstance<Services.IAsyncNavigationService>().NavigateAsync(typeof(SourceCodeView), Repository.FullName, Repository);

                                          }));
            }
        }

        private RelayCommand _profileTapped;
        public RelayCommand ProfileTapped
        {
            get
            {
                return _profileTapped
                    ?? (_profileTapped = new RelayCommand(
                                          () =>
                                          {
                                              SimpleIoc.Default.GetInstance<Services.IAsyncNavigationService>().NavigateAsync(typeof(DeveloperProfileView), "Profile", Repository.Owner.Login);
                                          }));
            }
        }

        private RelayCommand _issuesTapped;
        public RelayCommand IssuesTapped
        {
            get
            {
                return _issuesTapped
                    ?? (_issuesTapped = new RelayCommand(
                                          () =>
                                          {
                                              SimpleIoc.Default.GetInstance<Services.IAsyncNavigationService>().NavigateAsync(typeof(IssuesView), "Issues", Repository);
                                          }));
            }
        }

        private RelayCommand _StarCommand;
        public RelayCommand StarCommand
        {
            get
            {
                return _StarCommand
                    ?? (_StarCommand = new RelayCommand(
                                          async () =>
                                          {
                                              if (!IsStar)
                                              {
                                                  IsStarLoading = true;
                                                  if (await RepositoryUtility.StarRepository(Repository))
                                                  {
                                                      IsStarLoading = false;
                                                      IsStar = true;
                                                      GlobalHelper.NewStarActivity = true;
                                                  }
                                              }
                                              else
                                              {
                                                  IsStarLoading = true;
                                                  if (await RepositoryUtility.UnstarRepository(Repository))
                                                  {
                                                      IsStarLoading = false;
                                                      IsStar = false;
                                                      GlobalHelper.NewStarActivity = true;
                                                  }
                                              }
                                          }));
            }
        }

        private RelayCommand _WatchCommand;
        public RelayCommand WatchCommand
        {
            get
            {
                return _WatchCommand
                    ?? (_WatchCommand = new RelayCommand(
                                          async () =>
                                          {
                                              if (!IsWatching)
                                              {
                                                  IsWatchLoading = true;
                                                  if (await RepositoryUtility.WatchRepository(Repository))
                                                  {
                                                      IsWatchLoading = false;
                                                      IsWatching = true;
                                                  }
                                              }
                                              else
                                              {
                                                  IsWatchLoading = true;
                                                  if (await RepositoryUtility.UnwatchRepository(Repository))
                                                  {
                                                      IsWatchLoading = false;
                                                      IsWatching = false;
                                                  }
                                              }
                                          }));
            }
        }

        private RelayCommand _ForkCommand;
        public RelayCommand ForkCommand
        {
            get
            {
                return _ForkCommand
                    ?? (_ForkCommand = new RelayCommand(
                                          async () =>
                                          {
                                              IsForkLoading = true;
                                              Repository forkedRepo = await RepositoryUtility.ForkRepository(Repository);
                                              IsForkLoading = false;
                                              if (forkedRepo != null)
                                              {
                                                  SimpleIoc.Default.GetInstance<IAsyncNavigationService>().NavigateAsync(typeof(RepoDetailView), "Repository", forkedRepo);
                                              }
                                          }));
            }
        }
    }
}
