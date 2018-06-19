﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentValidation;
using ShareBook.Domain;
using ShareBook.Domain.Common;
using ShareBook.Domain.Enums;
using ShareBook.Helper.Extensions;
using ShareBook.Helper.Image;
using ShareBook.Repository;
using ShareBook.Repository.Infra;
using ShareBook.Service.Authorization;
using ShareBook.Service.CustomExceptions;
using ShareBook.Service.Generic;
using ShareBook.Service.Upload;

namespace ShareBook.Service
{
    public class BookService : BaseService<Book>, IBookService
    {
        private readonly IUploadService _uploadService;
        private readonly IBooksEmailService _booksEmailService;
        private readonly IBookUserService _bookUserService;

        public BookService(IBookRepository bookRepository, 
            IUnitOfWork unitOfWork, IValidator<Book> validator, 
            IUploadService uploadService, IBooksEmailService booksEmailService,
            IBookUserService bookUserService)
            : base(bookRepository, unitOfWork, validator)
        {
            _uploadService = uploadService;
            _booksEmailService = booksEmailService;
            _bookUserService = bookUserService;
        }

        [AuthorizationInterceptor(Permissions.Permission.AprovarLivro)]
        public Result<Book> Approve(Guid bookId)
        {
            var book = _repository.Get(bookId);
            if (book == null)
                throw new ShareBookException(ShareBookException.Error.NotFound);

            book.Approved = true;
            _repository.Update(book);

            return new Result<Book>(book);
        }

        public IList<dynamic> GetAllFreightOptions()
        {
            var enumValues = new List<dynamic>();
            foreach (FreightOption freightOption in Enum.GetValues(typeof(FreightOption)))
            {
                enumValues.Add(new
                {
                    Value = freightOption.ToString(),
                    Text = freightOption.Description()
                });
            }
            return enumValues;
        }

        public IList<Book> GetTop15NewBooks()
        {
            var listBookUsers = _bookUserService.Get();

            var query = (from book in _repository.Get()
                         where !(from booUser in listBookUsers
                                 select booUser.BookId)
                                 .Contains(book.Id)
                         where book.Approved == true
                         select book).OrderByDescending(x => x.CreationDate);

            var books = query.Take(15).ToList();
            return books;
        }

        public override Result<Book> Insert(Book entity)
        {
            entity.UserId = new Guid(Thread.CurrentPrincipal?.Identity?.Name);

            var result = Validate(entity);
            if (result.Success)
            {
                entity.Image = ImageHelper.FormatImageName(entity.Image, entity.Id.ToString());

                _uploadService.UploadImage(entity.ImageBytes, entity.Image);
                result.Value = _repository.Insert(entity);
                _booksEmailService.SendEmailNewBookInserted(entity).Wait();
            }
            return result;
        }

    }
}
