using FluentAssertions;
using PsarcImporter.Gui.ViewModels;
using Xunit;

namespace PsarcImporter.Tests;

public class GuiViewModelTests
{
    [Fact]
    public void PsarcFileItem_IsSelected_DefaultsToTrue()
    {
        var item = new PsarcFileItem("song.psarc", "/path/song.psarc");

        item.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void PsarcFileItem_SetIsSelected_FiresPropertyChanged()
    {
        var item = new PsarcFileItem("song.psarc", "/path/song.psarc");
        bool fired = false;
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PsarcFileItem.IsSelected))
                fired = true;
        };

        item.IsSelected = false;

        fired.Should().BeTrue();
        item.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void PsarcFileItem_FileName_IsSetCorrectly()
    {
        var item = new PsarcFileItem("mysong.psarc", "/path/mysong.psarc");

        item.FileName.Should().Be("mysong.psarc");
    }

    [Fact]
    public void PsarcFileItem_FilePath_IsSetCorrectly()
    {
        var item = new PsarcFileItem("song.psarc", "/some/path/song.psarc");

        item.FilePath.Should().Be("/some/path/song.psarc");
    }

    [Fact]
    public void ActionCommand_Execute_RunsAction()
    {
        bool executed = false;
        var command = new ActionCommand(() => executed = true);

        command.Execute(null);

        executed.Should().BeTrue();
    }

    [Fact]
    public void ActionCommand_CanExecute_AlwaysReturnsTrue()
    {
        var command = new ActionCommand(() => { });

        command.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void ActionCommand_MultipleExecutes_AllRun()
    {
        int count = 0;
        var command = new ActionCommand(() => count++);

        command.Execute(null);
        command.Execute(null);
        command.Execute(null);

        count.Should().Be(3);
    }
}
