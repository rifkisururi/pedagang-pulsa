using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Web.Controllers;
using PedagangPulsa.Web.Models;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Web.Controllers
{
    public class HomeControllerTests
    {
        private readonly HomeController _controller;

        public HomeControllerTests()
        {
            _controller = new HomeController();
        }

        [Fact]
        public void Index_ShouldReturnViewResult()
        {
            // Act
            var result = _controller.Index();

            // Assert
            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public void Privacy_ShouldReturnViewResult()
        {
            // Act
            var result = _controller.Privacy();

            // Assert
            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public void Error_WithCurrentActivity_ShouldReturnErrorViewModelWithActivityId()
        {
            // Arrange
            var activity = new Activity("TestActivity");
            activity.Start();

            try
            {
                // Act
                var result = _controller.Error();

                // Assert
                var viewResult = result.Should().BeOfType<ViewResult>().Subject;
                var model = viewResult.Model.Should().BeOfType<ErrorViewModel>().Subject;

                model.RequestId.Should().Be(activity.Id);
            }
            finally
            {
                activity.Stop();
            }
        }

        [Fact]
        public void Error_WithoutCurrentActivity_ShouldReturnErrorViewModelWithTraceIdentifier()
        {
            // Arrange
            var traceIdentifier = "TestTraceIdentifier";
            var httpContext = new DefaultHttpContext();
            httpContext.TraceIdentifier = traceIdentifier;

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = _controller.Error();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ErrorViewModel>().Subject;

            model.RequestId.Should().Be(traceIdentifier);
        }
    }
}
