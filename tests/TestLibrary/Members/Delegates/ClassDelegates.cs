namespace TestLibrary.Members.Delegates
{
  public class ClassDelegates
  {
    public delegate void DelegatePublic();
    internal delegate void DelegateInternal();
    protected delegate void DelegateProtected();
    protected internal delegate void DelegateProtectedInternal();
    public delegate string DelegateString();
    public delegate string DelegateArguments(string a, string b);
  }
}
