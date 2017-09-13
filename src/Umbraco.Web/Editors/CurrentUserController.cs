﻿using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using Umbraco.Core.Composing;
using Umbraco.Core.Services;
using Umbraco.Web.Models;
using Umbraco.Web.Models.ContentEditing;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;
using Umbraco.Core.Security;
using Umbraco.Web.WebApi.Filters;


namespace Umbraco.Web.Editors
{
    /// <summary>
    /// Controller to back the User.Resource service, used for fetching user data when already authenticated. user.service is currently used for handling authentication
    /// </summary>
    [PluginController("UmbracoApi")]
    public class CurrentUserController : UmbracoAuthorizedJsonController
    {

        /// <summary>
        /// When a user is invited and they click on the invitation link, they will be partially logged in
        /// where they can set their username/password
        /// </summary>
        /// <param name="newPassword"></param>
        /// <returns></returns>
        /// <remarks>
        /// This only works when the user is logged in (partially)
        /// </remarks>
        [WebApi.UmbracoAuthorize(requireApproval: false)]
        [OverrideAuthorization]
        public async Task<UserDetail> PostSetInvitedUserPassword([FromBody]string newPassword)
        {
            var result = await UserManager.AddPasswordAsync(Security.GetUserId(), newPassword);

            if (result.Succeeded == false)
            {
                //it wasn't successful, so add the change error to the model state, we've name the property alias _umb_password on the form
                // so that is why it is being used here.
                ModelState.AddModelError(
                    "value",
                    string.Join(", ", result.Errors));

                throw new HttpResponseException(Request.CreateValidationErrorResponse(ModelState));
            }

            //They've successfully set their password, we can now update their user account to be approved
            Security.CurrentUser.IsApproved = true;
            Services.UserService.Save(Security.CurrentUser);

            //now we can return their full object since they are now really logged into the back office
            var userDisplay = Mapper.Map<UserDetail>(Security.CurrentUser);
            var httpContextAttempt = TryGetHttpContext();
            if (httpContextAttempt.Success)
            {
                //set their remaining seconds
                userDisplay.SecondsUntilTimeout = httpContextAttempt.Result.GetRemainingAuthSeconds();
            }
            return userDisplay;
        }

        [AppendUserModifiedHeader]
        [FileUploadCleanupFilter(false)]
        public async Task<HttpResponseMessage> PostSetAvatar()
        {
            //borrow the logic from the user controller
            return await UsersController.PostSetAvatarInternal(Request, Services.UserService, Current.ApplicationCache.StaticCache, Security.GetUserId());
        }

        /// <summary>
        /// Changes the users password
        /// </summary>
        /// <param name="data"></param>
        /// <returns>
        /// If the password is being reset it will return the newly reset password, otherwise will return an empty value
        /// </returns>
        public async Task<ModelWithNotifications<string>> PostChangePassword(ChangingPasswordModel data)
        {
            var passwordChanger = new PasswordChanger(Logger, Services.UserService);
            var passwordChangeResult = await passwordChanger.ChangePasswordWithIdentityAsync(Security.CurrentUser, data, ModelState, UserManager);

            if (passwordChangeResult.Success)
            {
                //even if we weren't resetting this, it is the correct value (null), otherwise if we were resetting then it will contain the new pword
                var result = new ModelWithNotifications<string>(passwordChangeResult.Result.ResetPassword);
                result.AddSuccessNotification(Services.TextService.Localize("user/password"), Services.TextService.Localize("user/passwordChanged"));
                return result;
            }

            throw new HttpResponseException(Request.CreateValidationErrorResponse(ModelState));
        }

    }
}
