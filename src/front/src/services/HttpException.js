// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
class HttpException {
  constructor(code, message) {
    this.code = code
    this.message = message

    this.toString = function () {
      return this.code + this.message
    }
  }
}

  export default HttpException
