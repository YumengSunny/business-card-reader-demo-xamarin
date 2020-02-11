﻿using BCReaderDemo.Models;
using BCReaderDemo.Utils;
using DataService;
using Leadtools;
using Leadtools.Codecs;
using Leadtools.Controls;
using Leadtools.Demos.Utils;
using Leadtools.ImageProcessing.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace BCReaderDemo
{
   public enum ImageType
   {
      FrontImage,
      ProfileImage,
      BackImage,
   }

   public enum ImageOperations
   {
      Delete,
      Replace,
   }

   public class ImageModifiedEventArgs : EventArgs
   {
      public ImageType ImageType { get; set; }

      public ImageOperations ImageOperations { get; set; }

      public ContactModel ContactItem { get; set; }
      
      public ImageModifiedEventArgs(ContactModel contactItem, ImageType imageType, ImageOperations imageOperations)
      {
         this.ImageOperations = imageOperations;
         this.ImageType = imageType;
         this.ContactItem = contactItem;
      }
   }

   [XamlCompilation(XamlCompilationOptions.Compile)]
   public partial class ImageViewerPage : ContentPage
   {
      
      private ImageViewer _imageViewer;
      private RasterImage _rasterImage;
      private string _imagePath;
      private ImageType _imageType;
      private ContactModel _contactModel;
      private System.Timers.Timer _adsHiddenTimer = null;
      private System.Timers.Timer _adsVisibleTimer = null;

      public delegate void ImageModifiedEventHandler(object source, ImageModifiedEventArgs e);
      public event ImageModifiedEventHandler ImageModified;

      public ImageType ImageType { get => _imageType; set => _imageType = value; }

      public ImageViewerPage(ContactModel contactModel, string filePath, ImageType imageType)
      {
         InitializeComponent();

         _imagePath = filePath;
         _contactModel = contactModel;
         _imageType = imageType;
         
         Init();
      }

      public ImageViewerPage(ContactModel contactModel, RasterImage rasterImage, ImageType imageType)
      {
         InitializeComponent();

         _rasterImage = rasterImage;
         _contactModel = contactModel;
         _imageType = imageType;
                  
         Init();
      }

      private async void Init()
      {
         _imageViewer = new ImageViewer();
         _imageViewer.ViewHorizontalAlignment = ControlAlignment.Center;
         _imageViewer.ViewVerticalAlignment = ControlAlignment.Center;
         _imageViewer.BackgroundColor = Color.Transparent;
         _imageViewer.VerticalOptions = LayoutOptions.FillAndExpand;
         _imageViewer.HorizontalOptions = LayoutOptions.FillAndExpand;
         _imageViewer.Margin = new Thickness(10, PlatformsConstants.PagesHeaderTitleRowHeight, 10, PlatformsConstants.PagesHeaderTitleRowHeight);

         // Add PanZoom interactive mode to the ImageViewer control.
         _imageViewer.InteractiveModes.BeginUpdate();
         _imageViewer.InteractiveModes.Add(new ImageViewerPanZoomInteractiveMode { IsEnabled = true, DoubleTapSizeMode = ControlSizeMode.ActualSize });
         _imageViewer.InteractiveModes.EndUpdate();

         imageViewerGrid.Children.Add(_imageViewer, 0, 0);

         if(_imageType == ImageType.FrontImage)
         {
            deleteButton.IsEnabled = false;
            deleteButton.IsVisible = false;
         }

         if (!String.IsNullOrEmpty(_imagePath))
         {
            await Task.Run(async () =>
            {
               try
               {
                  _rasterImage = await RasterImageLoader.RasterImageFromFile(_imagePath);
               }
               catch(Exception ex)
               {
                  Console.WriteLine(ex.Message);
               }
            });
         }

         if (Device.IsInvokeRequired)
         {
            Device.BeginInvokeOnMainThread(() =>
            {
               _imageViewer.Image = _rasterImage;
               _imageViewer.Zoom(ControlSizeMode.FitAlways, 1.0, _imageViewer.DefaultZoomOrigin);
            });
         }
         else
         {
            _imageViewer.Image = _rasterImage;
            _imageViewer.Zoom(ControlSizeMode.FitAlways, 1.0, _imageViewer.DefaultZoomOrigin);
         }

         _adsHiddenTimer = new System.Timers.Timer(HomePage.AD_HIDDEN_DURATION);
         _adsHiddenTimer.AutoReset = true;
         _adsHiddenTimer.Elapsed += (sender, e) =>
         {
            _adsHiddenTimer.Enabled = false;
            _adsHiddenTimer.Stop();
            _adsVisibleTimer = AdHelper.ShowAdvertisement(advertisementLayout);
            _adsVisibleTimer.Elapsed += (sender1, e1) =>
            {
               _adsVisibleTimer.Enabled = false;
               _adsVisibleTimer = null;
               _adsHiddenTimer.Enabled = true;
               _adsHiddenTimer.Start();
            };
         };
      }

      protected override void OnAppearing()
      {
         base.OnAppearing();

         if (_adsHiddenTimer != null && (_adsVisibleTimer == null || (_adsVisibleTimer != null && !_adsVisibleTimer.Enabled)))
         {
            _adsHiddenTimer.Enabled = false;
            _adsHiddenTimer.Stop();
            _adsVisibleTimer = AdHelper.ShowAdvertisement(advertisementLayout);
            _adsVisibleTimer.Elapsed += (sender1, e1) =>
            {
               _adsVisibleTimer.Enabled = false;
               _adsVisibleTimer = null;
               _adsHiddenTimer.Enabled = true;
               _adsHiddenTimer.Start();
            };
         }
      }

      protected override void OnDisappearing()
      {
         base.OnDisappearing();

         if (_adsHiddenTimer != null)
         {
            _adsHiddenTimer.Stop();
            _adsHiddenTimer.Enabled = false;
         }
      }

      private void BackButton_Clicked(object sender, EventArgs e)
      {
         HomePage.Instance.Navigation.PopAsync();
      }

      private async void RetakeButton_Clicked(object sender, EventArgs e)
      {
         try
         {
            var results = await DependencyService.Get<Permissions.IPermissions>().VerifyPermissionsAsync(Permissions.Permission.Camera);
            if (results == null || results[Permissions.Permission.Camera] != Permissions.PermissionStatus.Granted)
               return;

            CameraPage cameraPage = new CameraPage(CameraOperationType.Normal, true);

            if (_imageType == ImageType.BackImage || _imageType == ImageType.FrontImage)
               cameraPage.CurrentOperation = CameraOperationType.BackImage;
            else
               cameraPage.CurrentOperation = CameraOperationType.Profile;

            cameraPage.PictureTaken += CameraPage_PictureTaken;
            HomePage.Instance.PushCameraPage(cameraPage);
         }
         catch(Exception ex)
         {
            Console.WriteLine(ex.Message);
         }
      }

      private async void CameraPage_PictureTaken(object source, PictureTakenEventArgs e)
      {
         try
         {
            string filePath = "";
            PictureSaveResolution saveResolution;

            if (_imageType == ImageType.ProfileImage)
            {
               filePath = _contactModel.ProfileImage;
               saveResolution = PictureSaveResolution.Low;
            }
            else if(_imageType == ImageType.BackImage)
            {
               filePath = _contactModel.BackImage;
               saveResolution = PictureSaveResolution.Medium;
            }
            else
            {
               filePath = _contactModel.Picture;

               if(String.IsNullOrEmpty(filePath))
               {
                  filePath = Path.Combine(HomePage.APP_DIR, $"image_{Guid.NewGuid()}.jpeg");
                  _contactModel.Picture = filePath;
               }

               saveResolution = PictureSaveResolution.Medium;
            }

            if (File.Exists(filePath))
               File.Delete(filePath);

            Stream imageStream = e.Stream;
            RasterImage image = e.Image;

            if (imageStream != null)
            {
               await DependencyService.Get<IPictureSaver>().SaveImage(imageStream, filePath, saveResolution);
            }
            else if (image != null)
            {
               LeadSize size = RasterImageHelper.GetImageSize(image.Width, image.Height, saveResolution);
               ResizeInterpolateCommand resizeInterpolateCommand = new ResizeInterpolateCommand(size.Width, size.Height, ResizeInterpolateCommandType.Resample);
               resizeInterpolateCommand.Run(image);

               using (var codecs = new RasterCodecs())
               {
                  codecs.Save(image, filePath, RasterImageFormat.Jpeg, 0);
               }
            }

            if(image != null)
            {
               _rasterImage = image;
            }
            else
            {
               await Task.Run(async () =>
               {
                  try
                  {
                     _rasterImage = await RasterImageLoader.RasterImageFromFile(filePath);
                  }
                  catch (Exception ex)
                  {
                     Console.WriteLine(ex.Message);
                  }
               });
            }

            if(_imageType == ImageType.FrontImage)
            {
               RasterImage rasterThumbnail = _rasterImage.CreateThumbnail(300, 200, _rasterImage.BitsPerPixel, RasterViewPerspective.TopLeft, RasterSizeFlags.Resample);
               using (var codecs = new RasterCodecs())
               {
                  string thumbnailPath = _contactModel.Thumbnail;
                  if (string.IsNullOrEmpty(thumbnailPath))
                  {
                     thumbnailPath = Path.Combine(HomePage.THUMBS_DIR, $"thumb_{Guid.NewGuid()}.jpeg");
                     _contactModel.Thumbnail = thumbnailPath;
                  }

                  codecs.Save(rasterThumbnail, thumbnailPath, RasterImageFormat.Jpeg, 0);
               }
            }

            Device.BeginInvokeOnMainThread(() =>
            {
               _imageViewer.Image = _rasterImage;
               _imageViewer.Zoom(ControlSizeMode.FitAlways, 1.0, _imageViewer.DefaultZoomOrigin);
            });

            ImageModified?.Invoke(this, new ImageModifiedEventArgs(this._contactModel, _imageType, ImageOperations.Replace));
         }
         catch (Exception ex)
         {
            Console.WriteLine(ex.Message);
         }
      }

      private void DeleteButton_Clicked(object sender, EventArgs e)
      {
         Device.BeginInvokeOnMainThread(async () =>
         {
            bool accept = await this.DisplayAlert("Delete Image", "Delete this image?", "Yes", "No");
            if (accept)
            {
               if (_imageType == ImageType.ProfileImage)
               {
                  if(File.Exists(_contactModel.ProfileImage))
                  {
                     File.Delete(_contactModel.ProfileImage);
                  }

                  _contactModel.ProfileImage = String.Empty;

                  ImageModified?.Invoke(this, new ImageModifiedEventArgs(this._contactModel, ImageType.ProfileImage, ImageOperations.Delete));

                  await HomePage.Instance.Navigation.PopAsync();
               }
               else if(_imageType == ImageType.BackImage)
               {
                  if (File.Exists(_contactModel.BackImage))
                  {
                     File.Delete(_contactModel.BackImage);
                  }

                  _contactModel.BackImage = String.Empty;

                  ImageModified?.Invoke(this, new ImageModifiedEventArgs(this._contactModel, ImageType.BackImage, ImageOperations.Delete));

                  await HomePage.Instance.Navigation.PopAsync();
               }
            }
         });               
      }

   }
}