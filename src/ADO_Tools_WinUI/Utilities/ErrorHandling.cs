using Microsoft.UI.Xaml;
//using System.Windows.Forms;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADO_Tools.Utilities
{
    class ErrorHandling
    {
        public void ErrorNoticeAsync(Exception exception, XamlRoot xamlRoot)
        {

            Exception activeException = GetInnerMostException(exception);

            string errorMessage = activeException.Message;
            ContentDialog errorDialog = new ContentDialog
            {
                Title = "Error",
                Content = errorMessage,
                CloseButtonText = "OK"
            };
            errorDialog.ShowAsync();
        }

        public static Exception[] GetInnerExceptions(Exception ex)
        {
            List<Exception> exceptions = new List<Exception>();
            exceptions.Add(ex);

            Exception currentEx = ex;
            while (currentEx.InnerException != null)
            {
                exceptions.Add(currentEx);
            }

            // Reverse the order to the innermost is first
            exceptions.Reverse();

            return exceptions.ToArray();
        }


        public static Exception GetInnerMostException(Exception ex)
        {
            Exception currentEx = ex;
            while (currentEx.InnerException != null)
            {
                currentEx = currentEx.InnerException;
            }

            return currentEx;
        }
    }



}

    
