// <copyright file="SigninSampleScript.cs" company="Google Inc.">
// Copyright (C) 2017 Google Inc. All Rights Reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations

  using System;
  using System.Collections.Generic;
  using System.Threading.Tasks;
  using Google;
    using TMPro;
    using UnityEngine;
  using UnityEngine.UI;

public class googleSignin : MonoBehaviour
{



  public string webClientId = "-.apps.googleusercontent.com";
  public static googleSignin Instance { get; private set; }

  private GoogleSignInConfiguration configuration;
  // ✅ 추가: 현재 로그인된 사용자
  public GoogleSignInUser CurrentUser { get; private set; }

  // ✅ 추가: 로그인 완료 콜백
  private Action<GoogleSignInUser> onSignInSuccess;
  private Action<string> onSignInError;


  // Defer the configuration creation until Awake so the web Client ID
  // Can be set via the property inspector in the Editor.
  void Awake()
  {
    Instance = this;
    configuration = new GoogleSignInConfiguration
    {
      WebClientId = webClientId,
      RequestIdToken = true
    };
  }


  public void OnSignInGoogle()
  {
    GoogleSignIn.Configuration = configuration;
    GoogleSignIn.Configuration.UseGameSignIn = false;
    GoogleSignIn.Configuration.RequestIdToken = true;

    GoogleSignIn.DefaultInstance.SignIn().ContinueWith(OnAuthenticationFinished);
  }

  // ✅ 추가: 비동기 로그인 메서드 (AuthManager용)
  public Task<GoogleSignInUser> SignInAsync()
  {
    var tcs = new TaskCompletionSource<GoogleSignInUser>();

    onSignInSuccess = user => tcs.SetResult(user);
    onSignInError = error => tcs.SetException(new Exception(error));

    GoogleSignIn.Configuration = configuration;
    GoogleSignIn.Configuration.UseGameSignIn = false;
    GoogleSignIn.Configuration.RequestIdToken = true;

    GoogleSignIn.DefaultInstance.SignIn().ContinueWith(OnAuthenticationFinished);

    return tcs.Task;
  }

  public void OnSignOut()
  {
  
    GoogleSignIn.DefaultInstance.SignOut();
    CurrentUser = null;
  }

  public void OnDisconnect()
  {

    GoogleSignIn.DefaultInstance.Disconnect();
    CurrentUser = null;
  }


  internal void OnAuthenticationFinished(Task<GoogleSignInUser> task)
  {
    if (task.IsFaulted)
    {
      using (IEnumerator<System.Exception> enumerator = task.Exception.InnerExceptions.GetEnumerator())
      {
        if (enumerator.MoveNext())
        {
          GoogleSignIn.SignInException error = (GoogleSignIn.SignInException)enumerator.Current;
          string errorMsg = "Got Error: " + error.Status + " " + error.Message;
  

          // ✅ 에러 콜백 호출
          onSignInError?.Invoke(errorMsg);
          onSignInError = null;
          onSignInSuccess = null;
        }
        else
        {
           onSignInError?.Invoke(task.Exception.ToString());
          onSignInError = null;
          onSignInSuccess = null;
        }
      }
    }
    else if (task.IsCanceled)
    {

      onSignInError?.Invoke("User canceled");
      onSignInError = null;
      onSignInSuccess = null;
    }
    else
    {
      CurrentUser = task.Result;

      // ✅ 성공 콜백 호출
      onSignInSuccess?.Invoke(task.Result);
      onSignInSuccess = null;
      onSignInError = null;
    }
  }

  public void OnSignInSilently()
  {
    GoogleSignIn.Configuration = configuration;
    GoogleSignIn.Configuration.UseGameSignIn = false;
    GoogleSignIn.Configuration.RequestIdToken = true;


    GoogleSignIn.DefaultInstance.SignInSilently().ContinueWith(OnAuthenticationFinished);
  }

  public void OnGamesSignIn()
  {
    GoogleSignIn.Configuration = configuration;
    GoogleSignIn.Configuration.UseGameSignIn = true;
    GoogleSignIn.Configuration.RequestIdToken = false;



    GoogleSignIn.DefaultInstance.SignIn().ContinueWith(OnAuthenticationFinished);
  }

  
}
    
